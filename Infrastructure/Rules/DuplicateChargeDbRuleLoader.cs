using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models.Rules;

namespace SettlementMcpServer.Infrastructure.Rules;

/// <summary>
/// 重复收费规则数据库加载器实现
/// </summary>
/// <remarks>
/// <para>
/// 从 DuckDB 数据库加载重复收费规则数据，用于运行时阶段。
/// 在初始化阶段使用 <see cref="DuplicateChargeExcelRuleLoader"/> 从 Excel 加载规则并写入数据库，
/// 运行时使用 <see cref="DuplicateChargeDbRuleLoader"/> 从数据库快速加载规则。
/// </para>
/// </remarks>
public sealed class DuplicateChargeDbRuleLoader : IRuleLoader
{
    private readonly IRuleRepository _ruleRepository;

    /// <summary>
    /// 初始化重复收费规则数据库加载器
    /// </summary>
    /// <param name="ruleRepository">规则仓储</param>
    public DuplicateChargeDbRuleLoader(IRuleRepository ruleRepository)
    {
        _ruleRepository = ruleRepository ?? throw new ArgumentNullException(nameof(ruleRepository));
    }

    /// <inheritdoc />
    public RuleCategory SupportedCategory => RuleCategory.DuplicateCharge;

    /// <inheritdoc />
    public async Task<IRuleSet> LoadRuleSetAsync(string filePath, CancellationToken cancellationToken = default)
    {
        // filePath 参数在这里实际上是 ruleCode
        var ruleCode = filePath;

        var ruleSet = await _ruleRepository.GetRuleSetByCodeAsync(ruleCode, cancellationToken);
        if (ruleSet == null)
        {
            throw new InvalidOperationException($"重复收费规则 {ruleCode} 不存在于数据库中");
        }

        return ruleSet;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IRuleSet>> LoadAllRuleSetsAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        // filePath 参数在这里被忽略，从数据库加载所有重复收费规则
        var ruleCodes = await _ruleRepository.GetAllRuleCodesAsync(cancellationToken);

        var ruleSets = new List<IRuleSet>();
        foreach (var ruleCode in ruleCodes)
        {
            var ruleSet = await _ruleRepository.GetRuleSetByCodeAsync(ruleCode, cancellationToken);
            if (ruleSet != null)
            {
                ruleSets.Add(ruleSet);
            }
        }

        return ruleSets.AsReadOnly();
    }
}
