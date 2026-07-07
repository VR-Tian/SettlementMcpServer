using Microsoft.Extensions.Logging;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;
using SettlementMcpServer.Models.Rules;

namespace SettlementMcpServer.Infrastructure.Rules;

/// <summary>
/// 规则组合执行器实现
/// </summary>
public class RuleCombinationExecutor : IRuleCombinationExecutor
{
    private readonly IEnumerable<IRuleExecutor> _executors;
    private readonly ILogger<RuleCombinationExecutor> _logger;

    public RuleCombinationExecutor(
        IEnumerable<IRuleExecutor> executors,
        ILogger<RuleCombinationExecutor> logger)
    {
        _executors = executors;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuleViolation>> ExecuteAsync(
        IReadOnlyList<IRuleSet> ruleSets,
        RuleCombination combination,
        IEnumerable<Settlement> settlements,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "开始执行规则组合，组合编码: {CombinationCode}，组合名称: {CombinationName}，执行模式: {Mode}，规则数量: {RuleCount}",
            combination.CombinationCode,
            combination.CombinationName,
            combination.Mode,
            combination.RuleCodes.Count);

        // 根据 RuleCodes 过滤规则
        var filteredRuleSets = FilterRuleSetsByCodes(ruleSets, combination.RuleCodes);

        if (filteredRuleSets.Count == 0)
        {
            _logger.LogWarning("未找到匹配的规则编码，组合编码: {CombinationCode}", combination.CombinationCode);
            return [];
        }

        var allViolations = new List<RuleViolation>();

        if (combination.Mode == ExecutionMode.Sequential)
        {
            // 顺序执行：依次执行每条规则
            foreach (var rs in filteredRuleSets)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var violations = await ExecuteSingleRuleAsync(rs, settlements, cancellationToken);
                allViolations.AddRange(violations);

                _logger.LogInformation(
                    "规则 {RuleCode} 执行完成，发现违规数量: {ViolationCount}",
                    rs.RuleCode,
                    violations.Count);
            }
        }
        else if (combination.Mode == ExecutionMode.Parallel)
        {
            // 并行执行：同时执行所有规则
            var tasks = filteredRuleSets.Select(rs =>
                ExecuteSingleRuleAsync(rs, settlements, cancellationToken));

            var results = await Task.WhenAll(tasks);

            foreach (var violations in results)
            {
                allViolations.AddRange(violations);
            }

            _logger.LogInformation(
                "并行执行完成，总违规数量: {ViolationCount}",
                allViolations.Count);
        }

        _logger.LogInformation(
            "规则组合执行完成，组合编码: {CombinationCode}，总违规数量: {ViolationCount}",
            combination.CombinationCode,
            allViolations.Count);

        return allViolations;
    }

    /// <summary>
    /// 根据规则编码过滤规则集
    /// </summary>
    /// <param name="ruleSets">完整规则集列表</param>
    /// <param name="ruleCodes">需要执行的规则编码列表</param>
    /// <returns>过滤后的规则集列表</returns>
    private List<IRuleSet> FilterRuleSetsByCodes(
        IReadOnlyList<IRuleSet> ruleSets,
        List<string> ruleCodes)
    {
        var result = new List<IRuleSet>();

        // 过滤出 RuleCode 在指定的 ruleCodes 列表中的规则集
        foreach (var ruleSet in ruleSets)
        {
            if (ruleCodes.Contains(ruleSet.RuleCode))
            {
                result.Add(ruleSet);
            }
        }

        return result;
    }

    /// <summary>
    /// 执行单条规则
    /// </summary>
    /// <param name="ruleSet">规则集</param>
    /// <param name="settlements">待审核的医保结算数据集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>违规结果列表</returns>
    private async Task<IReadOnlyList<RuleViolation>> ExecuteSingleRuleAsync(
        IRuleSet ruleSet,
        IEnumerable<Settlement> settlements,
        CancellationToken cancellationToken)
    {
        var executor = _executors.FirstOrDefault(e => e.SupportedCategory == ruleSet.Category);
        if (executor is null)
        {
            _logger.LogError(
                "找不到支持规则类别 {Category} 的执行器，规则编码: {RuleCode}",
                ruleSet.Category,
                ruleSet.RuleCode);

            throw new InvalidOperationException(
                $"找不到支持规则类别 {ruleSet.Category} 的执行器");
        }

        _logger.LogDebug(
            "已匹配执行器 {ExecutorType}，规则编码: {RuleCode}，规则类别: {Category}",
            executor.GetType().Name,
            ruleSet.RuleCode,
            ruleSet.Category);

        return await executor.ExecuteAsync(ruleSet, settlements, cancellationToken);
    }
}
