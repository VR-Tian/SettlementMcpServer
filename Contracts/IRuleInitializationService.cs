namespace SettlementMcpServer.Contracts;

/// <summary>
/// 规则初始化服务接口
/// </summary>
/// <remarks>
/// <para>
/// 负责从 Excel 规则文件扫描并写入 DuckDB 数据库，实现规则数据的初始化与增量更新。
/// </para>
/// </remarks>
public interface IRuleInitializationService
{
    /// <summary>
    /// 初始化规则数据（从 Excel 扫描并写入 DuckDB）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    Task InitializeRulesAsync(CancellationToken cancellationToken = default);
}
