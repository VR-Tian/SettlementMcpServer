namespace SettlementMcpServer.Models;

/// <summary>
/// 数据同步结果
/// </summary>
/// <remarks>
/// <para>
/// 包含数据同步操作的返回信息，包括同步的记录数和生成的 Parquet 文件路径。
/// </para>
/// </remarks>
public class DataSyncResult
{
    /// <summary>
    /// 同步的记录数
    /// </summary>
    public int RecordCount { get; set; }

    /// <summary>
    /// 生成的 Parquet 文件路径
    /// </summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>
    /// 同步的数据类型名称（如 "Settlement" 或 "AuditedResult"）
    /// </summary>
    public string DataTypeName { get; set; } = string.Empty;

    /// <summary>
    /// 同步完成时间
    /// </summary>
    public DateTime SyncTime { get; set; } = DateTime.Now;
}
