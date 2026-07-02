using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using SettlementMcpServer.Contracts;

namespace SettlementMcpServer.Tools;

/// <summary>
/// DuckDB 查询执行 MCP 工具
/// </summary>
/// <remarks>
/// <para>
/// 提供执行 DuckDB SQL 查询的功能。支持执行任意 SELECT 查询并返回 JSON 格式结果。
/// </para>
/// <para>
/// <b>前提条件：</b>
/// 必须先调用 <see cref="SyncDataToDuckDbTools"/> 中的方法同步数据到 DuckDB。
/// </para>
/// <para>
/// <b>使用流程：</b>
/// </para>
/// <list type="number">
///   <item>
///     <b>步骤 1：同步数据</b> - 调用 <see cref="SyncDataToDuckDbTools.SyncYuehaiSettlementDataAsync"/>
///     或 <see cref="SyncDataToDuckDbTools.SyncAuditedResultDataAsync"/>。
///   </item>
///   <item>
///     <b>步骤 2：获取 SQL</b> - 调用 <see cref="AnalysisDimensionTools.GetAnalysisSqlAsync"/>
///     获取分析维度的 SQL。
///   </item>
///   <item>
///     <b>步骤 3：执行查询</b> - 调用 <see cref="ExecuteDuckDbQueryAsync"/> 执行 SQL 查询。
///   </item>
/// </list>
/// </remarks>
internal class DuckDbQueryTools
{
    private readonly IDuckDbQueryService _duckDbQueryService;
    private readonly ILogger<DuckDbQueryTools> _logger;

    /// <summary>
    /// 初始化 DuckDB 查询工具
    /// </summary>
    /// <param name="duckDbQueryService">DuckDB 查询服务</param>
    /// <param name="logger">日志记录器</param>
    public DuckDbQueryTools(
        IDuckDbQueryService duckDbQueryService,
        ILogger<DuckDbQueryTools>? logger = null)
    {
        _duckDbQueryService = duckDbQueryService ?? throw new ArgumentNullException(nameof(duckDbQueryService));
        _logger = logger ?? NullLogger<DuckDbQueryTools>.Instance;
    }

    /// <summary>
    /// 执行 DuckDB SQL 查询
    /// </summary>
    /// <param name="sql">SQL 查询语句（SELECT 语句）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>查询结果的 JSON 字符串</returns>
    /// <exception cref="InvalidOperationException">数据尚未同步时抛出</exception>
    /// <remarks>
    /// <para>
    /// 执行 DuckDB SQL 查询并将结果序列化为 JSON 格式返回。
    /// 查询语句必须是 SELECT 语句，不支持 DML 操作。
    /// </para>
    /// <para>
    /// <b>可用视图：</b>
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>yuehai_settlements</c> - 医保结算数据据视图</description></item>
    ///   <item><description><c>audited_results</c> - 审核数据视图</description></item>
    /// </list>
    /// <para>
    /// <b>使用示例：</b>
    /// <code>
    /// var sql = """
    ///     SELECT 
    ///         InstitutionCode as 医院编码,
    ///         InstitutionName as 医院名称,
    ///         COUNT(*) as 就诊人次,
    ///         SUM(FeeDetailTotalAmount) as 费用总额
    ///     FROM yuehai_settlements
    ///     GROUP BY InstitutionCode, InstitutionName
    ///     ORDER BY 费用总额 DESC
    ///     LIMIT 10
    ///     """;
    /// 
    /// var result = await ExecuteDuckDbQueryAsync(sql);
    /// // result: JSON 格式的查询结果
    /// </code>
    /// </para>
    /// </remarks>
    [McpServerTool]
    [Description("执行 DuckDB SQL 查询，返回 JSON 格式结果。支持查询 yuehai_settlements 和 audited_results 视图")]
    public async Task<string> ExecuteDuckDbQueryAsync(
        [Description("SQL 查询语句（SELECT 语句），可使用 yuehai_settlements 和 audited_results 视图")]
        string sql,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException("SQL 查询语句不能为空", nameof(sql));
        }

        _logger.LogInformation("执行 DuckDB 查询: {Sql}", sql);

        // 检查数据是否已同步
        var dataExists = await _duckDbQueryService.EnsureDataExistsAsync(cancellationToken);
        if (!dataExists)
        {
            throw new InvalidOperationException(
                "DuckDB 中尚未同步数据，请先调用 SyncYuehaiSettlementDataAsync 或 SyncAuditedResultDataAsync 同步数据");
        }

        var result = await _duckDbQueryService.ExecuteQueryAsync(sql, cancellationToken);

        _logger.LogInformation("DuckDB 查询执行完成");

        return result;
    }
}
