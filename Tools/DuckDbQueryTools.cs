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
/// 提供执行 DuckDB 查询的功能。通过预定义的分析维度名称（唯一标识）执行对应的 SQL 查询，
/// 返回 JSON 格式结果。SQL 语句由服务端内部控制，不对外暴露。
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
///     <b>步骤 1：同步数据</b> - 调用 <see cref="SyncDataToDuckDbTools.SyncSettlementDataAsync"/>
///     或 <see cref="SyncDataToDuckDbTools.SyncAuditedResultDataAsync"/>。
///   </item>
///   <item>
///     <b>步骤 2：获取维度列表</b> - 调用
///     <see cref="AnalysisDimensionTools.GetAvailableAnalysisDimensionsAsync"/>
///     获取所有可用的分析维度名称。
///   </item>
///   <item>
///     <b>步骤 3：执行查询</b> - 调用 <see cref="ExecuteDuckDbQueryByDimensionAsync"/>
///     传入维度名称执行查询。
///   </item>
/// </list>
/// </remarks>
internal class DuckDbQueryTools
{
    private readonly IDuckDbQueryService _duckDbQueryService;
    private readonly IAnalysisSkillProvider _analysisSkillProvider;
    private readonly ILogger<DuckDbQueryTools> _logger;

    /// <summary>
    /// 初始化 DuckDB 查询工具
    /// </summary>
    /// <param name="duckDbQueryService">DuckDB 查询服务</param>
    /// <param name="analysisSkillProvider">分析维度提供者</param>
    /// <param name="logger">日志记录器</param>
    public DuckDbQueryTools(
        IDuckDbQueryService duckDbQueryService,
        IAnalysisSkillProvider analysisSkillProvider,
        ILogger<DuckDbQueryTools>? logger = null)
    {
        _duckDbQueryService = duckDbQueryService ?? throw new ArgumentNullException(nameof(duckDbQueryService));
        _analysisSkillProvider = analysisSkillProvider ?? throw new ArgumentNullException(nameof(analysisSkillProvider));
        _logger = logger ?? NullLogger<DuckDbQueryTools>.Instance;
    }

    /// <summary>
    /// 根据分析维度名称执行 DuckDB 查询
    /// </summary>
    /// <param name="dimensionName">分析维度名称（唯一标识），从 GetAvailableAnalysisDimensionsAsync 获取</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>查询结果的 JSON 字符串</returns>
    /// <exception cref="ArgumentException">维度名称不存在时抛出</exception>
    /// <exception cref="InvalidOperationException">数据尚未同步时抛出</exception>
    /// <remarks>
    /// <para>
    /// 根据维度名称查找服务端预定义的 SQL 查询模板并执行，SQL 语句不对外暴露。
    /// </para>
    /// <para>
    /// <b>可用视图：</b>
    /// </para>
    /// <list type="bullet">
    ///   <item><description><c>_settlements</c> - 医保结算数据视图</description></item>
    ///   <item><description><c>audited_results</c> - 审核数据视图</description></item>
    /// </list>
    /// </remarks>
    [McpServerTool]
    [Description("根据分析维度名称执行 DuckDB 查询，返回 JSON 格式结果。维度名称从 get_available_analysis_dimensions 获取")]
    public async Task<string> ExecuteDuckDbQueryByDimensionAsync(
        [Description("分析维度名称（唯一标识），从 get_available_analysis_dimensions 返回的 Name 字段获取")]
        string dimensionName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dimensionName);

        _logger.LogInformation("根据维度执行 DuckDB 查询: {DimensionName}", dimensionName);

        // 根据维度名称查找对应的 SQL 模板
        var dimension = _analysisSkillProvider.GetDimensionByName(dimensionName);
        if (dimension == null)
        {
            var availableNames = string.Join(", ", _analysisSkillProvider.GetAllDimensions().Select(d => d.Name));
            throw new ArgumentException(
                $"未找到名为 '{dimensionName}' 的分析维度。可用的维度名称: {availableNames}",
                nameof(dimensionName));
        }

        // 检查数据是否已同步
        var dataExists = await _duckDbQueryService.EnsureDataExistsAsync(cancellationToken);
        if (!dataExists)
        {
            throw new InvalidOperationException(
                "DuckDB 中尚未同步数据，请先调用 SyncSettlementDataAsync 或 SyncAuditedResultDataAsync 同步数据");
        }

        _logger.LogDebug("维度 '{DimensionName}' 对应的 SQL: {Sql}", dimensionName, dimension.SqlTemplate);

        var result = await _duckDbQueryService.ExecuteQueryAsync(dimension.SqlTemplate, cancellationToken);

        _logger.LogInformation("维度 '{DimensionName}' 的 DuckDB 查询执行完成", dimensionName);

        return result;
    }
}
