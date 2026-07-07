using System.Text.Json.Serialization;

namespace SettlementMcpServer.Models;

/// <summary>
/// 分析维度定义
/// </summary>
/// <remarks>
/// <para>
/// 包含分析维度的名称、描述和对应的 SQL 查询模板。
/// 用于 LLM 选择合适的分析视角。
/// </para>
/// <para>
/// <see cref="SqlTemplate"/> 属性通过 <see cref="JsonIgnoreAttribute"/> 标记，
/// 序列化时不会输出 SQL 语句，防止敏感查询逻辑泄露。
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
    /// <remarks>
    /// 序列化时忽略此属性，防止 SQL 语句泄露。
    /// </remarks>
    [JsonIgnore]
    public string SqlTemplate { get; set; } = string.Empty;

    /// <summary>
    /// 适用的数据类型（如 "Settlement" 或 "AuditedResult"）
    /// </summary>
    public string DataType { get; set; } = string.Empty;
}
