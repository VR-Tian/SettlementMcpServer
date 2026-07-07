using SettlementMcpServer.Models;
using SettlementMcpServer.Models.Rules;

namespace SettlementMcpServer.Contracts;

/// <summary>
/// 规则组合执行器接口
/// </summary>
/// <remarks>
/// <para>
/// 负责执行规则组合的批量审核逻辑，支持顺序执行和并行执行两种模式。
/// </para>
/// </remarks>
public interface IRuleCombinationExecutor
{
    /// <summary>
    /// 异步执行规则组合
    /// </summary>
    /// <param name="ruleSets">已加载的所有规则集列表</param>
    /// <param name="combination">规则组合定义</param>
    /// <param name="settlements">待审核的医保结算数据集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>所有规则的违规结果列表，无违规时返回空列表</returns>
    Task<IReadOnlyList<RuleViolation>> ExecuteAsync(
        IReadOnlyList<IRuleSet> ruleSets,
        RuleCombination combination,
        IEnumerable<Settlement> settlements,
        CancellationToken cancellationToken = default);
}
