using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;
using SettlementMcpServer.Models.Rules;

namespace SettlementMcpServer.Infrastructure.Rules.Executors;

/// <summary>
/// 限定频次规则执行器
/// </summary>
/// <remarks>
/// <para>
/// 负责执行限定频次规则的审核逻辑。根据 TimeIntervalType 采用不同的检测策略：
/// </para>
/// <list type="bullet">
///   <item><description>OneVisit (1)：一次就诊过程，直接判断结算单据的 Numbers 数量是否大于限定次数</description></item>
///   <item><description>Day (3)：天，根据 TimeInterval 数值计算当前项目编码在时间窗口内出现的次数</description></item>
///   <item><description>CustomDay (4)：自定义周期（按天数最小单位），根据 TimeInterval 数值计算当前项目编码在时间窗口内出现的次数</description></item>
///   <item><description>Month (6)：月，根据 TimeInterval 数值计算当前项目编码在时间窗口内出现的次数</description></item>
/// </list>
/// </remarks>
public sealed class FrequencyLimitExecutor : IRuleExecutor
{
    private readonly ILogger<FrequencyLimitExecutor> _logger;

    /// <summary>
    /// 初始化限定频次规则执行器
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public FrequencyLimitExecutor(ILogger<FrequencyLimitExecutor>? logger = null)
    {
        _logger = logger ?? NullLogger<FrequencyLimitExecutor>.Instance;
    }

    /// <inheritdoc />
    public RuleCategory SupportedCategory => RuleCategory.限定频次规则;

    /// <inheritdoc />
    public Task<IReadOnlyList<RuleViolation>> ExecuteAsync(
        IRuleSet ruleSet,
        IEnumerable<Settlement> settlements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(settlements);

        if (ruleSet is not FrequencyLimitRuleSet frequencyLimitRuleSet)
        {
            throw new ArgumentException($"规则集类型必须是 {nameof(FrequencyLimitRuleSet)}", nameof(ruleSet));
        }

        _logger.LogInformation(
            "开始执行限定频次规则审核，规则名称: {RuleName}，规则数量: {RuleCount}",
            frequencyLimitRuleSet.RuleName,
            frequencyLimitRuleSet.Rules.Count);

        var violations = new List<RuleViolation>();
        var materializedSettlements = settlements.ToList();

        // 遍历每条规则
        foreach (var rule in frequencyLimitRuleSet.Rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            _logger.LogDebug(
                "执行规则：项目名称: {ItemName}，项目编码: {ItemCode}，时间间隔类型: {TimeIntervalType}",
                rule.ItemName,
                rule.ItemCode,
                rule.TimeIntervalType);

            var ruleViolations = rule.TimeIntervalType switch
            {
                TimeIntervalType.OneVisit =>
                    ExecuteOneVisitCheck(frequencyLimitRuleSet, rule, materializedSettlements, cancellationToken),
                TimeIntervalType.Day =>
                    ExecuteDayWindowCheck(frequencyLimitRuleSet, rule, materializedSettlements, cancellationToken),
                TimeIntervalType.CustomDay =>
                    ExecuteCustomDayWindowCheck(frequencyLimitRuleSet, rule, materializedSettlements, cancellationToken),
                TimeIntervalType.Month =>
                    ExecuteMonthWindowCheck(frequencyLimitRuleSet, rule, materializedSettlements, cancellationToken),
                _ => throw new InvalidOperationException($"不支持的时间间隔类型: {rule.TimeIntervalType}")
            };

            violations.AddRange(ruleViolations);
        }

        _logger.LogInformation(
            "限定频次规则审核完成，规则名称: {RuleName}，发现 {ViolationCount} 条违规记录",
            frequencyLimitRuleSet.RuleName,
            violations.Count);

        return Task.FromResult<IReadOnlyList<RuleViolation>>(violations);
    }

    /// <summary>
    /// 执行一次就诊过程检测（OneVisit = 1）
    /// </summary>
    /// <remarks>
    /// 判断同一次就诊过程中（同一 VisitId）该项目编码出现的记录条数是否超过限定次数
    /// </remarks>
    private List<RuleViolation> ExecuteOneVisitCheck(
        FrequencyLimitRuleSet ruleSet,
        FrequencyLimitRule rule,
        List<Settlement> settlements,
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        var itemCodes = rule.ItemCodes;

        // 按人员编号 + VisitId 分组
        var groupedByVisit = settlements
            .Where(s => !string.IsNullOrEmpty(s.PersonnelNo) &&
                        !string.IsNullOrEmpty(s.VisitId) &&
                        !string.IsNullOrEmpty(s.MedicalCatalogCode) &&
                        itemCodes.Contains(s.MedicalCatalogCode))
            .GroupBy(s => new { s.PersonnelNo, s.VisitId });

        foreach (var visitGroup in groupedByVisit)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var visitSettlements = visitGroup.ToList();
            var countInVisit = visitSettlements.Count;
            var feeDate = ExtractDate(visitSettlements[0].FeeOccurrenceTime);

            // 判断住院限定
            if (rule.InpatientLimitCount.HasValue && countInVisit > rule.InpatientLimitCount.Value)
            {
                _logger.LogDebug(
                    "发现违规（一次就诊）：人员 {PersonnelNo} 在就诊 {VisitId} 的出现次数 {Count} 超过住院限定次数 {LimitCount}",
                    visitGroup.Key.PersonnelNo,
                    visitGroup.Key.VisitId,
                    countInVisit,
                    rule.InpatientLimitCount.Value);

                foreach (var settlement in visitSettlements)
                {
                    if (!violations.Any(v => v.PersonnelNo == settlement.PersonnelNo &&
                                            v.FeeOccurrenceDate == feeDate &&
                                            v.ViolationItemCode == settlement.MedicalCatalogCode))
                    {
                        violations.Add(CreateViolation(ruleSet, rule, settlement, feeDate,
                            visitSettlements));
                    }
                }
            }
            // 判断门诊限定
            else if (rule.OutpatientLimitCount.HasValue && countInVisit > rule.OutpatientLimitCount.Value)
            {
                _logger.LogDebug(
                    "发现违规（一次就诊）：人员 {PersonnelNo} 在就诊 {VisitId} 的出现次数 {Count} 超过门诊限定次数 {LimitCount}",
                    visitGroup.Key.PersonnelNo,
                    visitGroup.Key.VisitId,
                    countInVisit,
                    rule.OutpatientLimitCount.Value);

                foreach (var settlement in visitSettlements)
                {
                    if (!violations.Any(v => v.PersonnelNo == settlement.PersonnelNo &&
                                            v.FeeOccurrenceDate == feeDate &&
                                            v.ViolationItemCode == settlement.MedicalCatalogCode))
                    {
                        violations.Add(CreateViolation(ruleSet, rule, settlement, feeDate,
                            visitSettlements));
                    }
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// 执行天窗口检测（Day = 3）
    /// </summary>
    /// <remarks>
    /// 根据 TimeInterval 数值计算当前项目编码在时间窗口（天）内出现的次数
    /// </remarks>
    private List<RuleViolation> ExecuteDayWindowCheck(
        FrequencyLimitRuleSet ruleSet,
        FrequencyLimitRule rule,
        List<Settlement> settlements,
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        var itemCodes = rule.ItemCodes;
        var timeInterval = rule.TimeInterval;

        // 按人员编号分组
        var groupedByPerson = settlements
            .Where(s => !string.IsNullOrEmpty(s.PersonnelNo) &&
                        !string.IsNullOrEmpty(s.MedicalCatalogCode) &&
                        itemCodes.Contains(s.MedicalCatalogCode))
            .GroupBy(s => s.PersonnelNo);

        foreach (var personGroup in groupedByPerson)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var personSettlements = personGroup.ToList();

            // 按日期排序
            personSettlements.Sort((a, b) =>
            {
                var dateA = ParseDate(a.FeeOccurrenceTime);
                var dateB = ParseDate(b.FeeOccurrenceTime);
                return dateA.CompareTo(dateB);
            });

            // 对每条记录，检查其在时间窗口内的出现次数
            foreach (var settlement in personSettlements)
            {
                var currentDate = ParseDate(settlement.FeeOccurrenceTime);
                var windowStart = currentDate.AddDays(-timeInterval);
                var windowEnd = currentDate;

                // 统计时间窗口内的出现次数
                var windowSettlements = personSettlements
                    .Where(s =>
                    {
                        var sDate = ParseDate(s.FeeOccurrenceTime);
                        return sDate >= windowStart && sDate <= windowEnd;
                    })
                    .ToList();

                var countInWindow = windowSettlements.Count;

                var feeDate = ExtractDate(settlement.FeeOccurrenceTime);
                // 判断住院限定
                if (rule.InpatientLimitCount.HasValue && countInWindow > rule.InpatientLimitCount.Value)
                {
                    _logger.LogDebug(
                        "发现违规（天窗口）：人员 {PersonnelNo} 在 {FeeDate} 的时间窗口内出现次数 {Count} 超过住院限定次数 {LimitCount}",
                        settlement.PersonnelNo,
                        feeDate,
                        countInWindow,
                        rule.InpatientLimitCount.Value);

                    if (!violations.Any(v => v.PersonnelNo == settlement.PersonnelNo &&
                                            v.FeeOccurrenceDate == feeDate &&
                                            v.ViolationItemCode == settlement.MedicalCatalogCode))
                    {
                        violations.Add(CreateViolation(ruleSet, rule, settlement, feeDate,
                            windowSettlements));
                    }
                }
                // 判断门诊限定
                else if (rule.OutpatientLimitCount.HasValue && countInWindow > rule.OutpatientLimitCount.Value)
                {
                    _logger.LogDebug(
                        "发现违规（天窗口）：人员 {PersonnelNo} 在 {FeeDate} 的时间窗口内出现次数 {Count} 超过门诊限定次数 {LimitCount}",
                        settlement.PersonnelNo,
                        feeDate,
                        countInWindow,
                        rule.OutpatientLimitCount.Value);

                    if (!violations.Any(v => v.PersonnelNo == settlement.PersonnelNo &&
                                            v.FeeOccurrenceDate == feeDate &&
                                            v.ViolationItemCode == settlement.MedicalCatalogCode))
                    {
                        violations.Add(CreateViolation(ruleSet, rule, settlement, feeDate,
                            windowSettlements));
                    }
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// 执行自定义天窗口检测（CustomDay = 4）
    /// </summary>
    /// <remarks>
    /// 根据 TimeInterval 数值计算当前项目编码在自定义时间窗口（天）内出现的次数
    /// </remarks>
    private List<RuleViolation> ExecuteCustomDayWindowCheck(
        FrequencyLimitRuleSet ruleSet,
        FrequencyLimitRule rule,
        List<Settlement> settlements,
        CancellationToken cancellationToken)
    {
        // 逻辑与 Day 相同，只是语义上表示自定义周期
        return ExecuteDayWindowCheck(ruleSet, rule, settlements, cancellationToken);
    }

    /// <summary>
    /// 执行月窗口检测（Month = 6）
    /// </summary>
    /// <remarks>
    /// 根据 TimeInterval 数值计算当前项目编码在时间窗口（月）内出现的次数
    /// </remarks>
    private List<RuleViolation> ExecuteMonthWindowCheck(
        FrequencyLimitRuleSet ruleSet,
        FrequencyLimitRule rule,
        List<Settlement> settlements,
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        var itemCodes = rule.ItemCodes;
        var timeInterval = rule.TimeInterval;

        // 按人员编号分组
        var groupedByPerson = settlements
            .Where(s => !string.IsNullOrEmpty(s.PersonnelNo) &&
                        !string.IsNullOrEmpty(s.MedicalCatalogCode) &&
                        itemCodes.Contains(s.MedicalCatalogCode))
            .GroupBy(s => s.PersonnelNo);

        foreach (var personGroup in groupedByPerson)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var personSettlements = personGroup.ToList();

            // 按日期排序
            personSettlements.Sort((a, b) =>
            {
                var dateA = ParseDate(a.FeeOccurrenceTime);
                var dateB = ParseDate(b.FeeOccurrenceTime);
                return dateA.CompareTo(dateB);
            });

            // 对每条记录，检查其在时间窗口内的出现次数
            foreach (var settlement in personSettlements)
            {
                var currentDate = ParseDate(settlement.FeeOccurrenceTime);
                var windowStart = currentDate.AddMonths(-timeInterval);
                var windowEnd = currentDate;

                // 统计时间窗口内的出现次数
                var windowSettlements = personSettlements
                    .Where(s =>
                    {
                        var sDate = ParseDate(s.FeeOccurrenceTime);
                        return sDate >= windowStart && sDate <= windowEnd;
                    })
                    .ToList();

                var countInWindow = windowSettlements.Count;

                var feeDate = ExtractDate(settlement.FeeOccurrenceTime);

                // 判断住院限定
                if (rule.InpatientLimitCount.HasValue && countInWindow > rule.InpatientLimitCount.Value)
                {
                    _logger.LogDebug(
                        "发现违规（月窗口）：人员 {PersonnelNo} 在 {FeeDate} 的时间窗口内出现次数 {Count} 超过住院限定次数 {LimitCount}",
                        settlement.PersonnelNo,
                        feeDate,
                        countInWindow,
                        rule.InpatientLimitCount.Value);

                    if (!violations.Any(v => v.PersonnelNo == settlement.PersonnelNo &&
                                            v.FeeOccurrenceDate == feeDate &&
                                            v.ViolationItemCode == settlement.MedicalCatalogCode))
                    {
                        violations.Add(CreateViolation(ruleSet, rule, settlement, feeDate,
                            windowSettlements));
                    }
                }
                // 判断门诊限定
                else if (rule.OutpatientLimitCount.HasValue && countInWindow > rule.OutpatientLimitCount.Value)
                {
                    _logger.LogDebug(
                        "发现违规（月窗口）：人员 {PersonnelNo} 在 {FeeDate} 的时间窗口内出现次数 {Count} 超过门诊限定次数 {LimitCount}",
                        settlement.PersonnelNo,
                        feeDate,
                        countInWindow,
                        rule.OutpatientLimitCount.Value);

                    if (!violations.Any(v => v.PersonnelNo == settlement.PersonnelNo &&
                                            v.FeeOccurrenceDate == feeDate &&
                                            v.ViolationItemCode == settlement.MedicalCatalogCode))
                    {
                        violations.Add(CreateViolation(ruleSet, rule, settlement, feeDate,
                            windowSettlements));
                    }
                }
            }
        }

        return violations;
    }

    #region 辅助方法

    /// <summary>
    /// 从费用发生时间中提取日期部分（YYYYMMDD格式）
    /// </summary>
    /// <param name="feeOccurrenceTime">费用发生时间</param>
    /// <returns>日期字符串</returns>
    private static string ExtractDate(string? feeOccurrenceTime)
    {
        if (string.IsNullOrEmpty(feeOccurrenceTime))
        {
            return string.Empty;
        }

        // 尝试解析为 DateTime
        if (DateTime.TryParse(feeOccurrenceTime, out var dateTime))
        {
            return dateTime.ToString("yyyyMMdd");
        }

        // 如果已经是日期格式，直接返回
        if (feeOccurrenceTime.Length >= 8)
        {
            return feeOccurrenceTime[..8].Replace("-", "");
        }

        return feeOccurrenceTime;
    }

    /// <summary>
    /// 解析费用发生时间为 DateTime
    /// </summary>
    /// <param name="feeOccurrenceTime">费用发生时间</param>
    /// <returns>DateTime</returns>
    private static DateTime ParseDate(string? feeOccurrenceTime)
    {
        if (string.IsNullOrEmpty(feeOccurrenceTime))
        {
            return DateTime.MinValue;
        }

        if (DateTime.TryParse(feeOccurrenceTime, out var dateTime))
        {
            return dateTime;
        }

        return DateTime.MinValue;
    }

    /// <summary>
    /// 创建 RuleViolation 对象
    /// </summary>
    private static RuleViolation CreateViolation(
        FrequencyLimitRuleSet ruleSet,
        FrequencyLimitRule rule,
        Settlement settlement,
        string feeOccurrenceDate,
        IEnumerable<Settlement>? relatedSettlements = null)
    {
        return new RuleViolation
        {
            RuleName = ruleSet.RuleName,
            RuleCategory = RuleCategory.限定频次规则,
            PersonnelNo = settlement.PersonnelNo ?? string.Empty,
            FeeOccurrenceDate = feeOccurrenceDate,
            InstitutionCode = settlement.InstitutionCode ?? string.Empty,
            InstitutionName = settlement.InstitutionName ?? string.Empty,
            GroupCodeA = rule.ItemCode,
            GroupCodeB = string.Empty,
            ViolationItemCode = settlement.MedicalCatalogCode ?? string.Empty,
            ViolationItemName = settlement.InsuranceCatalogName ?? string.Empty,
            ViolationQuantity = settlement.Quantity,
            ViolationUnitPrice = settlement.UnitPrice,
            ViolationAmount = settlement.FeeDetailTotalAmount,
            PromptMessage = rule.PromptMessage,
            ReceivingDeptCode = settlement.ReceivingDeptCode,
            ReceivingDeptName = settlement.ReceivingDeptName,
            RelatedSettlements = relatedSettlements?.ToList() ?? []
        };
    }

    #endregion
}
