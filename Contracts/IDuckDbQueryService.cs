namespace SettlementMcpServer.Contracts;

/// <summary>
/// DuckDB 查询服务接口
/// </summary>
/// <remarks>
/// <para>
/// 负责执行 DuckDB SQL 查询并将结果序列化为 JSON 格式返回。
/// 支持执行任意 SELECT 查询语句。
/// </para>
/// </remarks>
public interface IDuckDbQueryService
{
    /// <summary>
    /// 执行 DuckDB 查询并返回 JSON 格式结果
    /// </summary>
    /// <param name="sql">SQL 查询语句</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>查询结果的 JSON 字符串</returns>
    /// <exception cref="InvalidOperationException">数据尚未同步时抛出</exception>
    Task<string> ExecuteQueryAsync(string sql, CancellationToken cancellationToken = default);

    /// <summary>
    /// 检查数据是否已同步到 DuckDB
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果数据已同步返回 true，否则返回 false</returns>
    Task<bool> EnsureDataExistsAsync(CancellationToken cancellationToken = default);
}
