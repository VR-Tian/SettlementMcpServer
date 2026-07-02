using SettlementMcpServer.Models;

namespace SettlementMcpServer.Contracts;

/// <summary>
/// 数据同步服务接口
/// </summary>
/// <remarks>
/// <para>
/// 负责将 Oracle 数据库中的数据同步到 DuckDB（通过 Parquet 文件中转）。
/// 支持同步医保结算数据据和审核数据两类数据。
/// </para>
/// </remarks>
public interface IDataSyncService
{
    /// <summary>
    /// 同步医保结算数据据到 DuckDB
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>同步结果，包含记录数和文件路径</returns>
    Task<DataSyncResult> SyncYuehaiSettlementsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 同步审核数据到 DuckDB
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>同步结果，包含记录数和文件路径</returns>
    Task<DataSyncResult> SyncAuditedResultsAsync(CancellationToken cancellationToken = default);
}
