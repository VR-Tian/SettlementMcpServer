using System.Net.Http.Json;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Microsoft.Extensions.Logging;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;
using SettlementMcpServer.Models.Rules;

namespace SettlementMcpServer.Infrastructure.Rules.Executors;

/// <summary>
/// 重复收费规则执行器
/// </summary>
/// <remarks>
/// <para>
/// 遍历内涵规则列表（<see cref="DuplicateChargeRule"/>），根据规则类型分发到对应的检测逻辑：
/// </para>
/// <list type="bullet">
///   <item><description>跨组共存（CrossGroupCoexist = 1）</description></item>
///   <item><description>组内重复低价（SameGroupDuplicate = 2）</description></item>
///   <item><description>阈值后存在（ThresholdThenExist = 3）</description></item>
///   <item><description>跨组数量阈值（CrossGroupQuantityThreshold = 4）</description></item>
/// </list>
/// </remarks>
public sealed class DuplicateChargeExecutor : IRuleExecutor
{
    private readonly ILogger<DuplicateChargeExecutor> _logger;

    /// <summary>
    /// 初始化重复收费规则执行器实例
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public DuplicateChargeExecutor(ILogger<DuplicateChargeExecutor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public RuleCategory SupportedCategory => RuleCategory.重复收费规则;

    /// <inheritdoc />
    public Task<IReadOnlyList<RuleViolation>> ExecuteAsync(
        IRuleSet ruleSet,
        IEnumerable<Settlement> settlements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(settlements);

        var dcRuleSet = (DuplicateChargeRuleSet)ruleSet;

        _logger.LogInformation(
            "开始执行重复收费规则 {RuleName}，内涵规则数: {RuleCount}",
            dcRuleSet.RuleName,
            dcRuleSet.Rules.Count);

        var allViolations = new List<RuleViolation>();
        var materializedSettlements = settlements.ToList();

        foreach (var rule in dcRuleSet.Rules)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // _logger.LogDebug(
            //     "执行内涵规则：状态={Status}，类型={RuleType}，GroupCodeA={GroupCodeA}，GroupCodeB={GroupCodeB}，A组项目数={GroupACount}，B组项目数={GroupBCount}",
            //     rule.Status, rule.RuleType, rule.GroupCodeA, rule.GroupCodeB,
            //     rule.GroupAItems.Count, rule.GroupBItems.Count);

            var violations = rule.RuleType switch
            {
                DuplicateChargeRuleType.CrossGroupCoexist =>
                    ExecuteCrossGroupCoexist(dcRuleSet, rule, materializedSettlements, cancellationToken),
                DuplicateChargeRuleType.SameGroupDuplicate =>
                    ExecuteSameGroupDuplicate(dcRuleSet, rule, materializedSettlements, cancellationToken),
                DuplicateChargeRuleType.ThresholdThenExist =>
                    ExecuteThresholdThenExist(dcRuleSet, rule, materializedSettlements, cancellationToken),
                DuplicateChargeRuleType.CrossGroupQuantityThreshold =>
                    ExecuteCrossGroupQuantityThreshold(dcRuleSet, rule, materializedSettlements, cancellationToken),
                _ => throw new InvalidOperationException($"不支持的规则类型: {rule.RuleType}")
            };

            allViolations.AddRange(violations);
        }

        _logger.LogInformation(
            "重复收费规则 {RuleName} 执行完成，共处理 {RuleCount} 条内涵规则，发现 {ViolationCount} 条违规",
            dcRuleSet.RuleName,
            dcRuleSet.Rules.Count,
            allViolations.Count);

        return Task.FromResult<IReadOnlyList<RuleViolation>>(allViolations);
    }

    /// <summary>
    /// 执行跨组共存检测（Type 1）
    /// </summary>
    /// <remarks>
    /// 逻辑：同一人同一天同组内，任一A组项目和任一B组项目同时收取，提示B组内所有项目违规
    /// </remarks>
    private List<RuleViolation> ExecuteCrossGroupCoexist(
        DuplicateChargeRuleSet ruleSet,
        DuplicateChargeRule rule,
        List<Settlement> settlements,
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        _logger.LogDebug(
            "开始执行跨组共存检测，规则ID {RuleName},A组项目数 {GroupACount}，B组项目数 {GroupBCount}",
            ruleSet.RuleName,
            rule.GroupAItems.Count,
            rule.GroupBItems.Count);
        var groupedSettlements = GroupSettlements(rule, settlements);

        foreach (var (groupKey, groupSettlements) in groupedSettlements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var groupAMatches = FindGroupAMatches(groupSettlements, rule.GroupAItems);
            var groupBMatches = FindGroupBMatches(groupSettlements, rule.GroupBItems);

            if (groupAMatches.Count > 0 && groupBMatches.Count > 0)
            {
                var (_, feeDate, deptCode) = ParseGroupKey(groupKey, rule.CheckSameExecDept ?? false);

                _logger.LogDebug(
                    "分组 {GroupKey} 发现跨组共存违规：A组匹配 {ACount} 条，B组匹配 {BCount} 条",
                    groupKey,
                    groupAMatches.Count,
                    groupBMatches.Count);

                foreach (var settlement in groupBMatches)
                {
                    violations.Add(CreateViolation(ruleSet, rule, settlement, feeDate, deptCode));
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// 执行组内重复低价检测（Type 2）
    /// </summary>
    /// <remarks>
    /// 逻辑：同A组内重复，同一天对项目收费单价低（除最高价以外项目）进行提示
    /// </remarks>
    private List<RuleViolation> ExecuteSameGroupDuplicate(
        DuplicateChargeRuleSet ruleSet,
        DuplicateChargeRule rule,
        List<Settlement> settlements,
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        var groupedSettlements = GroupSettlements(rule, settlements);

        foreach (var (groupKey, groupSettlements) in groupedSettlements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var groupAMatches = FindGroupAMatches(groupSettlements, rule.GroupAItems);

            if (groupAMatches.Count == 0)
            {
                continue;
            }

            // 按项目编码分组，找出每个项目编码的最高单价
            var byItemCode = groupAMatches
                .GroupBy(s => s.MedicalCatalogCode ?? string.Empty)
                .ToDictionary(
                    g => g.Key,
                    g => g.Max(s => s.UnitPrice ?? 0m));

            foreach (var settlement in groupAMatches)
            {
                var itemCode = settlement.MedicalCatalogCode ?? string.Empty;
                var currentPrice = settlement.UnitPrice ?? 0m;
                var maxPrice = byItemCode.GetValueOrDefault(itemCode, 0m);

                if (currentPrice < maxPrice)
                {
                    var (_, feeDate, deptCode) = ParseGroupKey(groupKey, rule.CheckSameExecDept ?? false);

                    _logger.LogDebug(
                        "分组 {GroupKey} 发现组内重复低价违规：项目 {ItemCode} 当前单价 {CurrentPrice}，最高单价 {MaxPrice}",
                        groupKey,
                        itemCode,
                        currentPrice,
                        maxPrice);

                    violations.Add(CreateViolation(ruleSet, rule, settlement, feeDate, deptCode));
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// 执行阈值后存在检测（Type 3）
    /// </summary>
    /// <remarks>
    /// 逻辑：先判断A组内项目总数量大于累计扣费阈值，再判断是否存在B组项目，若存在进行提示
    /// </remarks>
    private List<RuleViolation> ExecuteThresholdThenExist(
        DuplicateChargeRuleSet ruleSet,
        DuplicateChargeRule rule,
        List<Settlement> settlements,
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        var threshold = rule.DeductionThreshold ?? 0m;
        var groupedSettlements = GroupSettlements(rule, settlements);

        foreach (var (groupKey, groupSettlements) in groupedSettlements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var groupAMatches = FindGroupAMatches(groupSettlements, rule.GroupAItems);

            if (groupAMatches.Count == 0)
            {
                continue;
            }

            var totalAQuantity = groupAMatches.Sum(s => s.Quantity ?? 0m);

            if (totalAQuantity > threshold)
            {
                var groupBMatches = FindGroupBMatches(groupSettlements, rule.GroupBItems);

                if (groupBMatches.Count > 0)
                {
                    var (_, feeDate, deptCode) = ParseGroupKey(groupKey, rule.CheckSameExecDept ?? false);

                    _logger.LogDebug(
                        "分组 {GroupKey} 发现阈值后存在违规：A组总数量 {TotalAQuantity} > 阈值 {Threshold}，B组匹配 {BCount} 条",
                        groupKey,
                        totalAQuantity,
                        threshold,
                        groupBMatches.Count);

                    foreach (var settlement in groupBMatches)
                    {
                        violations.Add(CreateViolation(ruleSet, rule, settlement, feeDate, deptCode));
                    }
                }
            }
        }

        return violations;
    }

    /// <summary>
    /// 执行跨组数量阈值检测（Type 4）
    /// </summary>
    /// <remarks>
    /// 逻辑：同一人同一天同组内，A组和B组同时收取，且B组项目收费数量大于累计扣费阈值
    /// </remarks>
    private List<RuleViolation> ExecuteCrossGroupQuantityThreshold(
        DuplicateChargeRuleSet ruleSet,
        DuplicateChargeRule rule,
        List<Settlement> settlements,
        CancellationToken cancellationToken)
    {
        var violations = new List<RuleViolation>();
        var threshold = rule.DeductionThreshold ?? 0m;
        var groupedSettlements = GroupSettlements(rule, settlements);

        foreach (var (groupKey, groupSettlements) in groupedSettlements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var groupAMatches = FindGroupAMatches(groupSettlements, rule.GroupAItems);
            var groupBMatches = FindGroupBMatches(groupSettlements, rule.GroupBItems);

            if (groupAMatches.Count > 0 && groupBMatches.Count > 0)
            {
                var totalBQuantity = groupBMatches.Sum(s => s.Quantity ?? 0m);

                if (totalBQuantity > threshold)
                {
                    var (_, feeDate, deptCode) = ParseGroupKey(groupKey, rule.CheckSameExecDept ?? false);

                    _logger.LogDebug(
                        "分组 {GroupKey} 发现跨组数量阈值违规：A组匹配 {ACount} 条，B组总数量 {TotalBQuantity} > 阈值 {Threshold}，B组匹配 {BCount} 条",
                        groupKey,
                        groupAMatches.Count,
                        totalBQuantity,
                        threshold,
                        groupBMatches.Count);

                    foreach (var settlement in groupBMatches)
                    {
                        violations.Add(CreateViolation(ruleSet, rule, settlement, feeDate, deptCode));
                    }
                }
            }
        }

        return violations;
    }

    #region 辅助方法

    /// <summary>
    /// 按规则维度对结算数据进行分组
    /// </summary>
    private static Dictionary<string, List<Settlement>> GroupSettlements(
        DuplicateChargeRule rule,
        IEnumerable<Settlement> settlements)
    {
        var result = new Dictionary<string, List<Settlement>>();

        foreach (var settlement in settlements)
        {
            var feeDate = ExtractFeeDate(settlement.FeeOccurrenceTime);
            var key = rule.CheckSameExecDept != null && rule.CheckSameExecDept.Value
                ? $"{settlement.PersonnelNo}|{feeDate}|{settlement.ReceivingDeptCode}"
                : $"{settlement.PersonnelNo}|{feeDate}";

            if (!result.TryGetValue(key, out var list))
            {
                list = new List<Settlement>();
                result[key] = list;
            }

            list.Add(settlement);
        }

        return result;
    }

    /// <summary>
    /// 从 FeeOccurrenceTime 字符串中提取日期部分
    /// </summary>
    private static string ExtractFeeDate(string? feeOccurrenceTime)
    {
        if (string.IsNullOrWhiteSpace(feeOccurrenceTime))
        {
            return string.Empty;
        }

        if (DateTime.TryParse(feeOccurrenceTime, out var dateTime))
        {
            return dateTime.ToString("yyyy-MM-dd");
        }

        return feeOccurrenceTime.Length >= 10
            ? feeOccurrenceTime[..10]
            : feeOccurrenceTime;
    }

    /// <summary>
    /// 查找匹配A组项目的结算记录
    /// </summary>
    private static List<Settlement> FindGroupAMatches(
        IEnumerable<Settlement> settlements,
        IReadOnlyList<RuleGroupAItem> groupAItems)
    {
        var itemCodes = new HashSet<string>(
            groupAItems.Select(x => x.ItemCodeA),
            StringComparer.OrdinalIgnoreCase);

        return settlements
            .Where(s => !string.IsNullOrEmpty(s.MedicalCatalogCode)
                        && itemCodes.Contains(s.MedicalCatalogCode))
            .ToList();
    }

    /// <summary>
    /// 查找匹配B组项目的结算记录
    /// </summary>
    private static List<Settlement> FindGroupBMatches(
        IEnumerable<Settlement> settlements,
        IReadOnlyList<RuleGroupBItem> groupBItems)
    {
        var itemCodes = new HashSet<string>(
            groupBItems.Select(x => x.ItemCodeB),
            StringComparer.OrdinalIgnoreCase);

        return settlements
            .Where(s => !string.IsNullOrEmpty(s.MedicalCatalogCode)
                        && itemCodes.Contains(s.MedicalCatalogCode))
            .ToList();
    }

    /// <summary>
    /// 从分组键中解析出 PersonnelNo、FeeOccurrenceDate 和 ReceivingDeptCode
    /// </summary>
    private static (string PersonnelNo, string FeeOccurrenceDate, string? ReceivingDeptCode) ParseGroupKey(
        string groupKey,
        bool checkSameExecDept)
    {
        var parts = groupKey.Split('|');
        var personnelNo = parts.Length > 0 ? parts[0] : string.Empty;
        var feeDate = parts.Length > 1 ? parts[1] : string.Empty;
        var deptCode = checkSameExecDept && parts.Length > 2 ? parts[2] : null;

        return (personnelNo, feeDate, deptCode);
    }

    /// <summary>
    /// 创建 RuleViolation 对象
    /// </summary>
    private static RuleViolation CreateViolation(
        DuplicateChargeRuleSet ruleSet,
        DuplicateChargeRule rule,
        Settlement settlement,
        string feeOccurrenceDate,
        string? receivingDeptCode)
    {
        return new RuleViolation
        {
            RuleName = ruleSet.RuleName,
            RuleCategory = RuleCategory.重复收费规则,
            PersonnelNo = settlement.PersonnelNo ?? string.Empty,
            FeeOccurrenceDate = feeOccurrenceDate,
            InstitutionCode = settlement.InstitutionCode ?? string.Empty,
            InstitutionName = settlement.InstitutionName ?? string.Empty,
            GroupCodeA = rule.GroupCodeA,
            GroupCodeB = rule.GroupCodeB,
            ViolationItemCode = settlement.MedicalCatalogCode ?? string.Empty,
            ViolationItemName = settlement.InstitutionCatalogName ?? string.Empty,
            ViolationQuantity = settlement.Quantity,
            ViolationUnitPrice = settlement.UnitPrice,
            ViolationAmount = settlement.FeeDetailTotalAmount,
            PromptMessage = rule.PromptMessage
        };
    }
    #endregion
}