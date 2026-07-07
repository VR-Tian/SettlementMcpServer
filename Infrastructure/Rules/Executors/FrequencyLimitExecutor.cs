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
/// 负责执行限定频次规则的审核逻辑。
/// </para>
/// <para>
/// 规则逻辑：
/// </para>
/// <list type="number">
///   <item>聚合汇总当前费用单据同一天费用日期（YYYYMMDD）的多个项目编码（以分隔符'|'组合）总数量</item>
///   <item>聚合当前费用单据多个项目编码（以分隔符'|'组合）总数量平均数量超出住院天数</item>
///   <item>聚合汇总当前费用单据同一天费用日期（YYYYMMDD）的项目编码总数量</item>
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
    public RuleCategory SupportedCategory => RuleCategory.FrequencyLimit;

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

        var rule = frequencyLimitRuleSet.Rule;
        var itemCodes = frequencyLimitRuleSet.ItemCodes;

        _logger.LogInformation(
            "开始执行限定频次规则审核，规则编码: {RuleCode}，项目名称: {ItemName}，项目编码数量: {ItemCount}",
            rule.RuleCode,
            rule.ItemName,
            itemCodes.Count);

        var violations = new List<RuleViolation>();

        // 按人员编号 + 费用发生日期分组
        var groupedSettlements = settlements
            .Where(s => !string.IsNullOrEmpty(s.PersonnelNo) && 
                        !string.IsNullOrEmpty(s.FeeOccurrenceTime))
            .GroupBy(s => new
            {
                s.PersonnelNo,
                FeeDate = ExtractDate(s.FeeOccurrenceTime)
            })
            .ToList();

        _logger.LogDebug("数据分组完成，共 {GroupCount} 个分组", groupedSettlements.Count);

        foreach (var group in groupedSettlements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var personnelNo = group.Key.PersonnelNo;
            var feeDate = group.Key.FeeDate;

            // 筛选当前分组中包含的项目
            var groupItems = group
                .Where(s => itemCodes.Contains(s.InsuranceCatalogCode ?? string.Empty))
                .ToList();

            if (groupItems.Count == 0)
            {
                continue;
            }

            // 规则1：聚合汇总同一天多个项目编码的总数量
            var totalQuantity = groupItems.Sum(s => s.Quantity ?? 0);

            // 规则2：平均数量是否超出住院天数
            if (rule.InpatientLimitCount.HasValue && rule.InpatientLimitCount.Value > 0)
            {
                var hospitalDays = groupItems.First().HospitalDays ?? 1;
                var avgQuantityPerDay = totalQuantity / hospitalDays;

                if (avgQuantityPerDay > rule.InpatientLimitCount.Value)
                {
                    _logger.LogDebug(
                        "发现违规：人员 {PersonnelNo} 在 {FeeDate} 的日均数量 {AvgQuantity} 超过限定次数 {LimitCount}",
                        personnelNo,
                        feeDate,
                        avgQuantityPerDay,
                        rule.InpatientLimitCount.Value);

                    foreach (var item in groupItems)
                    {
                        violations.Add(new RuleViolation
                        {
                            RuleCode = rule.RuleCode,
                            RuleCategory = RuleCategory.FrequencyLimit,
                            PersonnelNo = personnelNo,
                            FeeOccurrenceDate = feeDate,
                            InstitutionCode = item.InstitutionCode ?? string.Empty,
                            InstitutionName = item.InstitutionName ?? string.Empty,
                            GroupCodeA = string.Join("|", itemCodes),
                            GroupCodeB = string.Empty,
                            ViolationItemCode = item.InsuranceCatalogCode ?? string.Empty,
                            ViolationItemName = item.InsuranceCatalogName ?? string.Empty,
                            ViolationQuantity = item.Quantity,
                            ViolationUnitPrice = item.UnitPrice,
                            ViolationAmount = item.FeeDetailTotalAmount,
                            PromptMessage = rule.PromptMessage,
                            ReceivingDeptCode = item.ReceivingDeptCode,
                            ReceivingDeptName = item.ReceivingDeptName
                        });
                    }
                }
            }

            // 规则3：同一天项目编码总数量是否超限
            if (rule.OutpatientLimitCount.HasValue && rule.OutpatientLimitCount.Value > 0)
            {
                if (totalQuantity > rule.OutpatientLimitCount.Value)
                {
                    _logger.LogDebug(
                        "发现违规：人员 {PersonnelNo} 在 {FeeDate} 的总数量 {TotalQuantity} 超过限定次数 {LimitCount}",
                        personnelNo,
                        feeDate,
                        totalQuantity,
                        rule.OutpatientLimitCount.Value);

                    foreach (var item in groupItems)
                    {
                        // 避免重复添加（如果已经在规则2中添加了）
                        if (!violations.Any(v => v.PersonnelNo == personnelNo && 
                                                  v.FeeOccurrenceDate == feeDate && 
                                                  v.ViolationItemCode == item.InsuranceCatalogCode))
                        {
                            violations.Add(new RuleViolation
                            {
                                RuleCode = rule.RuleCode,
                                RuleCategory = RuleCategory.FrequencyLimit,
                                PersonnelNo = personnelNo,
                                FeeOccurrenceDate = feeDate,
                                InstitutionCode = item.InstitutionCode ?? string.Empty,
                                InstitutionName = item.InstitutionName ?? string.Empty,
                                GroupCodeA = string.Join("|", itemCodes),
                                GroupCodeB = string.Empty,
                                ViolationItemCode = item.InsuranceCatalogCode ?? string.Empty,
                                ViolationItemName = item.InsuranceCatalogName ?? string.Empty,
                                ViolationQuantity = item.Quantity,
                                ViolationUnitPrice = item.UnitPrice,
                                ViolationAmount = item.FeeDetailTotalAmount,
                                PromptMessage = rule.PromptMessage,
                                ReceivingDeptCode = item.ReceivingDeptCode,
                                ReceivingDeptName = item.ReceivingDeptName
                            });
                        }
                    }
                }
            }
        }

        _logger.LogInformation(
            "限定频次规则审核完成，规则编码: {RuleCode}，发现 {ViolationCount} 条违规记录",
            rule.RuleCode,
            violations.Count);

        return Task.FromResult<IReadOnlyList<RuleViolation>>(violations);
    }

    /// <summary>
    /// 从费用发生时间中提取日期部分（YYYYMMDD格式）
    /// </summary>
    /// <param name="feeOccurrenceTime">费用发生时间</param>
    /// <returns>日期字符串</returns>
    private static string ExtractDate(string feeOccurrenceTime)
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
}
