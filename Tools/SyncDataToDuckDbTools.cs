using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;

namespace SettlementMcpServer.Tools;

/// <summary>
/// 数据同步到 DuckDB 的 MCP 工具
/// </summary>
/// <remarks>
/// <para>
/// 提供将 Oracle 数据同步到 DuckDB 的功能。同步过程会将数据导出为 Parquet 文件，
/// 然后在 DuckDB 中注册为可查询视图，以支持高性能的多维度分析查询。
/// </para>
/// <para>
/// <b>使用流程：</b>
/// </para>
/// <list type="number">
///   <item>
///     <b>步骤 1：同步数据</b> - 调用 <see cref="SyncYuehaiSettlementDataAsync"/> 或
///     <see cref="SyncAuditedResultDataAsync"/> 将数据同步到 DuckDB。
///   </item>
///   <item>
///     <b>步骤 2：执行分析</b> - 使用 <see cref="AnalysisDimensionTools"/> 获取分析维度，
///     然后使用 <see cref="DuckDbQueryTools"/> 执行查询。
///   </item>
/// </list>
/// </remarks>
internal class SyncDataToDuckDbTools
{
    private readonly IDataSyncService _dataSyncService;
    private readonly ILogger<SyncDataToDuckDbTools> _logger;

    /// <summary>
    /// 初始化数据同步工具
    /// </summary>
    /// <param name="dataSyncService">数据同步服务</param>
    /// <param name="logger">日志记录器</param>
    public SyncDataToDuckDbTools(
        IDataSyncService dataSyncService,
        ILogger<SyncDataToDuckDbTools>? logger = null)
    {
        _dataSyncService = dataSyncService ?? throw new ArgumentNullException(nameof(dataSyncService));
        _logger = logger ?? NullLogger<SyncDataToDuckDbTools>.Instance;
    }

    /// <summary>
    /// 同步医保结算数据据到 DuckDB
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>同步结果，包含记录数和文件路径</returns>
    /// <remarks>
    /// <para>
    /// 此方法将 Oracle 中的医保结算数据据导出为 Parquet 文件，并在 DuckDB 中注册为
    /// <c>yuehai_settlements</c> 视图，供后续分析查询使用。
    /// </para>
    /// <para>
    /// <b>使用示例：</b>
    /// <code>
    /// var result = await SyncYuehaiSettlementDataAsync();
    /// // result.RecordCount: 同步的记录数
    /// // result.FilePath: Parquet 文件路径
    /// </code>
    /// </para>
    /// </remarks>
    [McpServerTool]
    [Description("同步医保结算数据据到 DuckDB，将数据导出为 Parquet 文件并注册为可查询视图")]
    public async Task<DataSyncResult> SyncYuehaiSettlementDataAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始同步医保结算数据据到 DuckDB");

        var result = await _dataSyncService.SyncYuehaiSettlementsAsync(cancellationToken);

        _logger.LogInformation(
            "医保结算数据据同步完成，同步 {Count} 条记录，文件路径: {FilePath}",
            result.RecordCount, result.FilePath);

        return result;
    }

    /// <summary>
    /// 同步审核数据到 DuckDB
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>同步结果，包含记录数和文件路径</returns>
    /// <remarks>
    /// <para>
    /// 此方法将 Oracle 中的审核数据导出为 Parquet 文件，并在 DuckDB 中注册为
    /// <c>audited_results</c> 视图，供后续分析查询使用。
    /// </para>
    /// <para>
    /// <b>使用示例：</b>
    /// <code>
    /// var result = await SyncAuditedResultDataAsync();
    /// // result.RecordCount: 同步的记录数
    /// // result.FilePath: Parquet 文件路径
    /// </code>
    /// </para>
    /// </remarks>
    [McpServerTool]
    [Description("同步审核数据到 DuckDB，将数据导出为 Parquet 文件并注册为可查询视图")]
    public async Task<DataSyncResult> SyncAuditedResultDataAsync(
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始同步审核数据到 DuckDB");

        var result = await _dataSyncService.SyncAuditedResultsAsync(cancellationToken);

        _logger.LogInformation(
            "审核数据同步完成，同步 {Count} 条记录，文件路径: {FilePath}",
            result.RecordCount, result.FilePath);

        return result;
    }
}
