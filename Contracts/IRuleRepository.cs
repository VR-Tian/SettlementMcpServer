using SettlementMcpServer.Models.Rules;

namespace SettlementMcpServer.Contracts;

/// <summary>
/// 规则仓储接口
/// </summary>
/// <remarks>
/// <para>
/// 负责规则数据的持久化操作，支持从数据库加载规则集、保存规则集以及获取所有规则编码。
/// 支持多种规则类别（重复收费、限定频次等）。
/// </para>
/// </remarks>
public interface IRuleRepository
{
    /// <summary>
    /// 根据规则编码获取规则集
    /// </summary>
    /// <param name="ruleCode">规则编码（负页清单编码）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>规则集聚合根，如果不存在则返回 null</returns>
    /// <remarks>
    /// 根据数据库中存储的规则类别自动反序列化为对应的规则集类型。
    /// </remarks>
    Task<IRuleSet?> GetRuleSetByCodeAsync(string ruleCode, CancellationToken cancellationToken = default);

    /// <summary>
    /// 保存规则集
    /// </summary>
    /// <param name="ruleSet">规则集聚合根</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <remarks>
    /// 根据规则集的类别自动存储到对应的表中。
    /// </remarks>
    Task SaveRuleSetAsync(IRuleSet ruleSet, CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取所有规则编码
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>所有规则编码的只读列表</returns>
    Task<IReadOnlyList<string>> GetAllRuleCodesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据规则类别获取规则编码列表
    /// </summary>
    /// <param name="category">规则类别</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>指定类别的规则编码列表</returns>
    Task<IReadOnlyList<string>> GetRuleCodesByCategoryAsync(RuleCategory category, CancellationToken cancellationToken = default);
}
