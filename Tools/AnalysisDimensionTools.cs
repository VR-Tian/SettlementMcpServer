using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;

namespace SettlementMcpServer.Tools;

/// <summary>
/// 分析维度 MCP 工具
/// </summary>
/// <remarks>
/// <para>
/// 提供预定义的分析维度列表，供 LLM 选择合适的分析视角。
/// 每个维度包含名称、描述和对应的 SQL 查询模板（DuckDB 语法）。
/// </para>
/// <para>
/// <b>使用流程：</b>
/// </para>
/// <list type="number">
///   <item>
///     <b>步骤 1：获取维度列表</b> - 调用 <see cref="GetAvailableAnalysisDimensionsAsync"/>
///     获取所有可用的分析维度。
///   </item>
///   <item>
///     <b>步骤 2：获取 SQL</b> - 调用 <see cref="GetAnalysisSqlAsync"/> 获取指定维度的 SQL。
///   </item>
///   <item>
///     <b>步骤 3：执行查询</b> - 使用 <see cref="DuckDbQueryTools.ExecuteDuckDbQueryAsync"/>
///     执行 SQL 查询。
///   </item>
/// </list>
/// </remarks>
internal class AnalysisDimensionTools
{
    private readonly IAnalysisSkillProvider _analysisSkillProvider;
    private readonly ILogger<AnalysisDimensionTools> _logger;

    /// <summary>
    /// 初始化分析维度工具
    /// </summary>
    /// <param name="analysisSkillProvider">分析维度提供者</param>
    /// <param name="logger">日志记录器</param>
    public AnalysisDimensionTools(
        IAnalysisSkillProvider analysisSkillProvider,
        ILogger<AnalysisDimensionTools>? logger = null)
    {
        _analysisSkillProvider = analysisSkillProvider ?? throw new ArgumentNullException(nameof(analysisSkillProvider));
        _logger = logger ?? NullLogger<AnalysisDimensionTools>.Instance;
    }

    /// <summary>
    /// 获取所有可用的分析维度
    /// </summary>
    /// <returns>分析维度列表，包含名称、描述和数据类型</returns>
    /// <remarks>
    /// <para>
    /// 返回的分析维度包括：
    /// </para>
    /// <list type="bullet">
    ///   <item><description>医院维度统计（结算金额、违规数量）</description></item>
    ///   <item><description>科室维度统计（就诊人次、住院天数）</description></item>
    ///   <item><description>险种类型统计（费用分布）</description></item>
    ///   <item><description>时间维度统计（月度趋势）</description></item>
    ///   <item><description>诊断维度统计（高频疾病）</description></item>
    ///   <item><description>医疗类别统计（费用分布）</description></item>
    ///   <item><description>规则维度统计（违规类型）</description></item>
    /// </list>
    /// </remarks>
    [McpServerTool]
    [Description("获取所有可用的分析维度列表，每个维度包含名称、描述和适用的数据类型")]
    public Task<IReadOnlyList<AnalysisDimension>> GetAvailableAnalysisDimensionsAsync()
    {
        _logger.LogDebug("获取可用分析维度列表");

        var dimensions = _analysisSkillProvider.GetAllDimensions();

        _logger.LogDebug("返回 {Count} 个分析维度", dimensions.Count);

        return Task.FromResult(dimensions);
    }

    /// <summary>
    /// 获取指定分析维度的 SQL 查询语句
    /// </summary>
    /// <param name="dimensionName">维度名称（从 GetAvailableAnalysisDimensionsAsync 获取）</param>
    /// <returns>SQL 查询语句（DuckDB 语法）</returns>
    /// <exception cref="ArgumentException">维度名称不存在时抛出</exception>
    /// <remarks>
    /// <para>
    /// 获取指定维度的 SQL 查询模板，可直接用于 <see cref="DuckDbQueryTools.ExecuteDuckDbQueryAsync"/>
    /// 执行查询。
    /// </para>
    /// <para>
    /// <b>使用示例：</b>
    /// <code>
    /// // 步骤 1：获取维度列表
    /// var dimensions = await GetAvailableAnalysisDimensionsAsync();
    /// // 找到 "hospital_settlement_summary" 维度
    /// 
    /// // 步骤 2：获取 SQL
    /// var sql = await GetAnalysisSqlAsync("hospital_settlement_summary");
    /// // sql: "SELECT InstitutionCode as 医院编码, ..."
    /// 
    /// // 步骤 3：执行查询
    /// var result = await ExecuteDuckDbQueryAsync(sql);
    /// </code>
    /// </para>
    /// </remarks>
    [McpServerTool]
    [Description("获取指定分析维度的 SQL 查询语句，可直接用于 DuckDB 查询执行")]
    public Task<string> GetAnalysisSqlAsync(
        [Description("分析维度名称，从 GetAvailableAnalysisDimensionsAsync 返回的 Name 字段获取")]
        string dimensionName)
    {
        if (string.IsNullOrWhiteSpace(dimensionName))
        {
            throw new ArgumentException("维度名称不能为空", nameof(dimensionName));
        }

        _logger.LogDebug("获取分析维度 SQL: {DimensionName}", dimensionName);

        var dimension = _analysisSkillProvider.GetDimensionByName(dimensionName);

        if (dimension == null)
        {
            var availableNames = string.Join(", ", _analysisSkillProvider.GetAllDimensions().Select(d => d.Name));
            throw new ArgumentException(
                $"未找到名为 '{dimensionName}' 的分析维度。可用的维度名称: {availableNames}",
                nameof(dimensionName));
        }

        _logger.LogDebug("返回维度 '{DimensionName}' 的 SQL 模板", dimensionName);

        return Task.FromResult(dimension.SqlTemplate);
    }
}
