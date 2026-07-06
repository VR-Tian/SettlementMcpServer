using SettlementMcpServer.Models.Rules;

namespace SettlementMcpServer.Contracts;

/// <summary>
/// 规则加载器接口
/// </summary>
/// <remarks>
/// <para>
/// 负责从外部数据源（如 Excel 规则内涵文件）加载规则集。
/// 每种规则类别对应一个加载器实现，通过 <see cref="SupportedCategory"/> 属性区分。
/// </para>
/// </remarks>
public interface IRuleLoader
{
    /// <summary>
    /// 获取当前加载器支持的规则类别
    /// </summary>
    /// <returns>支持的 <see cref="RuleCategory"/> 枚举值</returns>
    RuleCategory SupportedCategory { get; }

    /// <summary>
    /// 从指定文件路径异步加载规则集
    /// </summary>
    /// <param name="filePath">规则内涵文件的完整路径</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>加载的规则集</returns>
    Task<IRuleSet> LoadRuleSetAsync(
        string filePath,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 从指定目录异步加载所有规则集
    /// </summary>
    /// <param name="directoryPath">规则文件所在目录</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>加载的所有规则集列表</returns>
    Task<IReadOnlyList<IRuleSet>> LoadAllRuleSetsAsync(
        string directoryPath,
        CancellationToken cancellationToken = default);
}
