using SettlementMcpServer.Models;
using SettlementMcpServer.Models.Rules;

namespace SettlementMcpServer.Contracts;

/// <summary>
/// 规则执行器接口
/// </summary>
/// <remarks>
/// <para>
/// 负责执行规则审核逻辑。每种规则类别对应一个执行器实现，
/// 通过 <see cref="SupportedCategory"/> 属性区分。
/// </para>
/// </remarks>
public interface IRuleExecutor
{
    /// <summary>
    /// 获取当前执行器支持的规则类别
    /// </summary>
    /// <returns>支持的 <see cref="RuleCategory"/> 枚举值</returns>
    RuleCategory SupportedCategory { get; }

    /// <summary>
    /// 异步执行规则审核
    /// </summary>
    /// <param name="ruleSet">待执行的规则集</param>
    /// <param name="settlements">待审核的医保结算数据集合</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>违规结果列表，无违规时返回空列表</returns>
    Task<IReadOnlyList<RuleViolation>> ExecuteAsync(
        IRuleSet ruleSet,
        IEnumerable<YuehaiSettlement> settlements,
        CancellationToken cancellationToken = default);
}
