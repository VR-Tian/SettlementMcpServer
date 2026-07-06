using Microsoft.Extensions.Logging;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;
using SettlementMcpServer.Models.Rules;

namespace SettlementMcpServer.Infrastructure.Rules.Executors;

/// <summary>
/// 跨组共存规则执行器（DuplicateChargeRuleType.CrossGroupCoexist = 1）
/// </summary>
/// <remarks>
/// <para>
/// 逻辑：同一人同一天同组内，任一A组项目和任一B组项目同时收取，提示B组内所有项目违规
/// </para>
/// <list type="number">
///   <item><description>在分组内，查找结算数据中 MedicalCatalogCode 匹配 GroupAItems 中 ItemCodeA 的记录（A组匹配记录）</description></item>
///   <item><description>同时查找匹配 GroupBItems 中 ItemCodeB 的记录（B组匹配记录）</description></item>
///   <item><description>如果A组和B组都有匹配记录，则所有B组匹配记录都生成 RuleViolation</description></item>
/// </list>
/// </remarks>
public sealed class CrossGroupCoexistExecutor : IRuleExecutor
{
    private readonly ILogger<CrossGroupCoexistExecutor> _logger;

    /// <summary>
    /// 初始化跨组共存规则执行器实例
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public CrossGroupCoexistExecutor(ILogger<CrossGroupCoexistExecutor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public RuleCategory SupportedCategory => RuleCategory.DuplicateCharge;

    /// <inheritdoc />
    public Task<IReadOnlyList<RuleViolation>> ExecuteAsync(
        IRuleSet ruleSet,
        IEnumerable<YuehaiSettlement> settlements,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        ArgumentNullException.ThrowIfNull(settlements);

        var dcRuleSet = (DuplicateChargeRuleSet)ruleSet;
        var violations = new List<RuleViolation>();
        var rule = dcRuleSet.Rule;

        _logger.LogDebug(
            "开始执行跨组共存规则 {RuleCode}，A组项目数: {GroupACount}，B组项目数: {GroupBCount}",
            rule.RuleCode,
            dcRuleSet.GroupAItems.Count,
            dcRuleSet.GroupBItems.Count);

        // 按规则维度分组
        var groupedSettlements = GroupSettlements(rule, settlements);

        foreach (var (groupKey, groupSettlements) in groupedSettlements)
        {
            cancellationToken.ThrowIfCancellationRequested();

            // 查找A组匹配记录
            var groupAMatches = FindGroupAMatches(groupSettlements, dcRuleSet.GroupAItems);

            // 查找B组匹配记录
            var groupBMatches = FindGroupBMatches(groupSettlements, dcRuleSet.GroupBItems);

            // 如果A组和B组都有匹配记录，则所有B组匹配记录都生成违规
            if (groupAMatches.Count > 0 && groupBMatches.Count > 0)
            {
                var (_, feeDate, deptCode) = ParseGroupKey(groupKey, rule.CheckSameExecDept);

                _logger.LogDebug(
                    "分组 {GroupKey} 发现跨组共存违规：A组匹配 {ACount} 条，B组匹配 {BCount} 条",
                    groupKey,
                    groupAMatches.Count,
                    groupBMatches.Count);

                foreach (var settlement in groupBMatches)
                {
                    var violation = CreateViolation(dcRuleSet, settlement, feeDate, deptCode);
                    violations.Add(violation);
                }
            }
        }

        _logger.LogInformation(
            "跨组共存规则 {RuleCode} 执行完成，发现 {ViolationCount} 条违规",
            rule.RuleCode,
            violations.Count);

        return Task.FromResult<IReadOnlyList<RuleViolation>>(violations);
    }

    /// <summary>
    /// 按规则维度对结算数据进行分组
    /// </summary>
    private static Dictionary<string, List<YuehaiSettlement>> GroupSettlements(
        DuplicateChargeRule rule,
        IEnumerable<YuehaiSettlement> settlements)
    {
        var result = new Dictionary<string, List<YuehaiSettlement>>();

        foreach (var settlement in settlements)
        {
            var feeDate = ExtractFeeDate(settlement.FeeOccurrenceTime);
            var key = rule.CheckSameExecDept
                ? $"{settlement.PersonnelNo}|{feeDate}|{settlement.ReceivingDeptCode}"
                : $"{settlement.PersonnelNo}|{feeDate}";

            if (!result.TryGetValue(key, out var list))
            {
                list = new List<YuehaiSettlement>();
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
    private static List<YuehaiSettlement> FindGroupAMatches(
        IEnumerable<YuehaiSettlement> settlements,
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
    private static List<YuehaiSettlement> FindGroupBMatches(
        IEnumerable<YuehaiSettlement> settlements,
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
        YuehaiSettlement settlement,
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
