using SettlementMcpServer.Models;
using SettlementMcpServer.Models.Rules;

namespace SettlementMcpServer.Contracts;

/// <summary>
/// 规则管道接口
/// </summary>
/// <remarks>
/// <para>
/// 负责编排规则引擎的完整执行流程：加载规则、分派执行、收集结果。
/// 作为规则引擎的门面（Facade），为上层调用提供统一的入口。
/// </para>
/// </remarks>
public interface IRulePipeline
{
    /// <summary>
    /// 异步执行规则管道
    /// </summary>
    /// <param name="ruleCode">规则编码</param>
    /// <param name="settlements">待审核的医保结算数据集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>所有违规结果列表，无违规时返回空列表</returns>
    /// <remarks>
    /// <para>
    /// 管道 SHALL 完成以下编排流程：
    /// </para>
    /// <list type="number">
    ///   <item><description>根据规则编码从数据库加载规则集</description></item>
    ///   <item><description>根据规则类别分派到对应的 <see cref="IRuleExecutor"/></description></item>
    ///   <item><description>收集并返回所有违规结果</description></item>
    /// </list>
    /// </remarks>
    Task<IReadOnlyList<RuleViolation>> ExecuteAsync(
        string ruleCode,
        IEnumerable<Settlement> settlements,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 异步执行规则组合管道
    /// </summary>
    /// <param name="ruleCodes">规则编码列表</param>
    /// <param name="combination">规则组合定义</param>
    /// <param name="settlements">待审核的医保结算数据集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>所有违规结果列表，无违规时返回空列表</returns>
    Task<IReadOnlyList<RuleViolation>> ExecuteCombinationAsync(
        IReadOnlyList<string> ruleCodes,
        RuleCombination combination,
        IEnumerable<Settlement> settlements,
        CancellationToken cancellationToken = default);
}
