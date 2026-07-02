using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SettlementMcpServer.Contracts;

namespace SettlementMcpServer.Infrastructure.DuckDb;

/// <summary>
/// DuckDB 查询服务实现
/// </summary>
/// <remarks>
/// <para>
/// 执行 DuckDB SQL 查询并将结果序列化为 JSON 格式返回。
/// 使用 <see cref="IDbConnectionFactory"/> 创建数据库连接。
/// </para>
/// </remarks>
public sealed class DuckDbQueryService : IDuckDbQueryService
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<DuckDbQueryService> _logger;

    /// <summary>
    /// 初始化 DuckDB 查询服务
    /// </summary>
    /// <param name="connectionFactory">DuckDB 连接工厂</param>
    /// <param name="logger">日志记录器</param>
    public DuckDbQueryService(
        [FromKeyedServices("duckdb")] IDbConnectionFactory connectionFactory,
        ILogger<DuckDbQueryService>? logger = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? NullLogger<DuckDbQueryService>.Instance;
    }

    /// <inheritdoc />
    public async Task<string> ExecuteQueryAsync(string sql, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException("SQL 查询语句不能为空", nameof(sql));
        }

        _logger.LogDebug("执行 DuckDB 查询: {Sql}", sql);

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        using var command = connection.CreateCommand();
        command.CommandText = sql;

        var dbCommand = (DbCommand)command;
        using var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);
        var results = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new Dictionary<string, object?>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                var columnName = reader.GetName(i);
                var value = reader.IsDBNull(i) ? null : reader.GetValue(i);
                row[columnName] = value;
            }
            results.Add(row);
        }

        _logger.LogDebug("DuckDB 查询完成，返回 {Count} 条记录", results.Count);

        // 序列化为 JSON
        var options = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        return JsonSerializer.Serialize(results, options);
    }

    /// <inheritdoc />
    public async Task<bool> EnsureDataExistsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            connection.Open();

            // 检查是否存在 yuehai_settlements 或 audited_results 视图
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(*) FROM information_schema.tables 
                WHERE table_name IN ('yuehai_settlements', 'audited_results')
                """;

            var dbCommand = (DbCommand)command;
            var count = Convert.ToInt32(await dbCommand.ExecuteScalarAsync(cancellationToken));
            return count > 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "检查 DuckDB 数据时发生异常");
            return false;
        }
    }
}
