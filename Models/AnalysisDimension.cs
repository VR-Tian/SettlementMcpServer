namespace SettlementMcpServer.Models;

/// <summary>
/// 分析维度定义
/// </summary>
/// <remarks>
/// <para>
/// 包含分析维度的名称、描述和对应的 SQL 查询模板。
/// 用于 LLM 选择合适的分析视角。
/// </para>
/// </remarks>
public class AnalysisDimension
{
    /// <summary>
    /// 维度名称（唯一标识）
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 维度描述
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// SQL 查询模板（DuckDB 语法）
    /// </summary>
    public string SqlTemplate { get; set; } = string.Empty;

    /// <summary>
    /// 适用的数据类型（如 "YuehaiSettlement" 或 "AuditedResult"）
    /// </summary>
    public string DataType { get; set; } = string.Empty;
}
