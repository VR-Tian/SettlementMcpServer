using Microsoft.Extensions.Logging;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;
using SettlementMcpServer.Models.Rules;

namespace SettlementMcpServer.Infrastructure.Rules.Executors;

/// <summary>
/// 组内重复低价规则执行器（DuplicateChargeRuleType.SameGroupDuplicate = 2）
/// </summary>
/// <remarks>
/// <para>
/// 逻辑：同A组内重复，同一天对项目收费单价低（除最高价以外项目）进行提示
/// </para>
/// <list type="number">
///   <item><description>在分组内，查找匹配A组项目的记录</description></item>
///   <item><description>按项目编码分组，找出每个项目编码的最高单价</description></item>
///   <item><description>除最高单价的项目外，其余记录生成 RuleViolation</description></item>
///   <item><description>不涉及B组</description></item>
/// </list>
/// </remarks>
public sealed class SameGroupDuplicateExecutor : IRuleExecutor
{
    private readonly ILogger<SameGroupDuplicateExecutor> _logger;

    /// <summary>
    /// 初始化组内重复低价规则执行器实例
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public SameGroupDuplicateExecutor(ILogger<SameGroupDuplicateExecutor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public RuleCategory SupportedCategory => RuleCategory.DuplicateCharge;

    /// <inheritdoc />
    public Task<IReadOnlyList<RuleViolation>> ExecuteAsync(
        IRuleSet ruleSet,
        IEnumerable<Settlement> settlements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(settlements);

        var dcRuleSet = (DuplicateChargeRuleSet)ruleSet;
        var violations = new List<RuleViolation>();
        var rule = dcRuleSet.Rule;

        _logger.LogDebug(
            "开始执行组内重复低价规则 {RuleCode}，A组项目数: {GroupACount}",
            rule.RuleCode,
            dcRuleSet.GroupAItems.Count);

        // 按规则维度分组
        var groupedSettlements = GroupSettlements(rule, settlements);

        foreach (var (groupKey, groupSettlements) in groupedSettlements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 查找A组匹配记录
            var groupAMatches = FindGroupAMatches(groupSettlements, dcRuleSet.GroupAItems);

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

            // 除最高单价的项目外，其余记录生成违规
            foreach (var settlement in groupAMatches)
            {
                var itemCode = settlement.MedicalCatalogCode ?? string.Empty;
                var currentPrice = settlement.UnitPrice ?? 0m;
                var maxPrice = byItemCode.GetValueOrDefault(itemCode, 0m);

                // 如果当前单价不是最高单价，则生成违规
                if (currentPrice < maxPrice)
                {
                    var (_, feeDate, deptCode) = ParseGroupKey(groupKey, rule.CheckSameExecDept);

                    _logger.LogDebug(
                        "分组 {GroupKey} 发现组内重复低价违规：项目 {ItemCode} 当前单价 {CurrentPrice}，最高单价 {MaxPrice}",
                        groupKey,
                        itemCode,
                        currentPrice,
                        maxPrice);

                    var violation = CreateViolation(dcRuleSet, settlement, feeDate, deptCode);
                    violations.Add(violation);
                }
            }
        }

        _logger.LogInformation(
            "组内重复低价规则 {RuleCode} 执行完成，发现 {ViolationCount} 条违规",
            rule.RuleCode,
            violations.Count);

        return Task.FromResult<IReadOnlyList<RuleViolation>>(violations);
    }

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
            var key = rule.CheckSameExecDept
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
        Settlement settlement,
        string feeOccurrenceDate,
        string? receivingDeptCode)
    {
        return new RuleViolation
        {
            RuleCode = ruleSet.Rule.RuleCode,
            RuleCategory = RuleCategory.DuplicateCharge,
            PersonnelNo = settlement.PersonnelNo ?? string.Empty,
            FeeOccurrenceDate = feeOccurrenceDate,
            InstitutionCode = settlement.InstitutionCode ?? string.Empty,
            InstitutionName = settlement.InstitutionName ?? string.Empty,
            GroupCodeA = ruleSet.Rule.GroupCodeA,
            GroupCodeB = ruleSet.Rule.GroupCodeB,
            ViolationItemCode = settlement.MedicalCatalogCode ?? string.Empty,
            ViolationItemName = settlement.InstitutionCatalogName ?? string.Empty,
            ViolationQuantity = settlement.Quantity,
            ViolationUnitPrice = settlement.UnitPrice,
            ViolationAmount = settlement.FeeDetailTotalAmount,
            PromptMessage = ruleSet.Rule.PromptMessage,
            ReceivingDeptCode = receivingDeptCode ?? settlement.ReceivingDeptCode,
            ReceivingDeptName = settlement.ReceivingDeptName
        };
    }
}
