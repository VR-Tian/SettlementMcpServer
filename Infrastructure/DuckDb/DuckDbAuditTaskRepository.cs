using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;
using TaskStatus = SettlementMcpServer.Models.TaskStatus;

namespace SettlementMcpServer.Infrastructure.DuckDb;

/// <summary>
/// DuckDB 审核任务仓储实现
/// </summary>
public sealed class DuckDbAuditTaskRepository : IAuditTaskRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<DuckDbAuditTaskRepository> _logger;

    /// <summary>
    /// 初始化 DuckDB 审核任务仓储
    /// </summary>
    /// <param name="connectionFactory">DuckDB 连接工厂</param>
    /// <param name="logger">日志记录器</param>
    public DuckDbAuditTaskRepository(
        [FromKeyedServices("duckdb")] IDbConnectionFactory connectionFactory,
        ILogger<DuckDbAuditTaskRepository>? logger = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? NullLogger<DuckDbAuditTaskRepository>.Instance;
    }

    /// <inheritdoc />
    public async Task SaveTaskAsync(AuditTask task, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(task);

        _logger.LogInformation("保存审核任务 {TaskId}，规则名称: {RuleName}", task.TaskId, task.RuleName);

        var connection = _connectionFactory.CreateConnection();

        await EnsureTableExistsAsync(connection, cancellationToken);

        const string insertSql = """
            INSERT INTO audit_tasks (
                task_id, rule_name, hospital_code, status,
                total_count, processed_count, violation_count,
                created_at, completed_at, error_message
            ) VALUES (
                @TaskId, @RuleName, @HospitalCode, @Status,
                @TotalCount, @ProcessedCount, @ViolationCount,
                @CreatedAt, @CompletedAt, @ErrorMessage
            )
            """;

        await connection.ExecuteAsync(insertSql, task, commandType: CommandType.Text);

        _logger.LogInformation("审核任务 {TaskId} 已保存", task.TaskId);
    }

    /// <inheritdoc />
    public async Task UpdateTaskStatusAsync(
        string taskId,
        TaskStatus status,
        string? errorMessage = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        _logger.LogInformation("更新任务 {TaskId} 状态为 {Status}", taskId, status);

        var connection = _connectionFactory.CreateConnection();

        await EnsureTableExistsAsync(connection, cancellationToken);

        var completedAt = status == TaskStatus.Completed || status == TaskStatus.Failed
            ? DateTime.Now
            : (DateTime?)null;

        const string updateSql = """
            UPDATE audit_tasks
            SET status = @Status,
                error_message = @ErrorMessage,
                completed_at = CASE WHEN @CompletedAt IS NOT NULL THEN @CompletedAt ELSE completed_at END
            WHERE task_id = @TaskId
            """;

        await connection.ExecuteAsync(updateSql, new
        {
            TaskId = taskId,
            Status = status.ToString(),
            ErrorMessage = errorMessage,
            CompletedAt = completedAt
        }, commandType: CommandType.Text);
    }

    /// <inheritdoc />
    public async Task<AuditTask?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        _logger.LogDebug("查询任务 {TaskId}", taskId);

        var connection = _connectionFactory.CreateConnection();

        await EnsureTableExistsAsync(connection, cancellationToken);

        const string sql = "SELECT * FROM audit_tasks WHERE task_id = @TaskId";
        var task = await connection.QueryFirstOrDefaultAsync<AuditTaskDto>(
            sql, new { TaskId = taskId });

        return task == null ? null : MapToAuditTask(task);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditTask>> GetTasksByRuleNameAsync(
        string ruleName,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleName);

        _logger.LogDebug("根据规则名称查询任务: {RuleName}", ruleName);

        var connection = _connectionFactory.CreateConnection();

        await EnsureTableExistsAsync(connection, cancellationToken);

        const string sql = "SELECT * FROM audit_tasks WHERE rule_name = @RuleName ORDER BY created_at DESC";
        var tasks = await connection.QueryAsync<AuditTaskDto>(sql, new { RuleName = ruleName });

        return tasks.Select(MapToAuditTask).ToList();
    }

    /// <summary>
    /// 确保 audit_tasks 表存在
    /// </summary>
    private async Task EnsureTableExistsAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS audit_tasks (
                task_id VARCHAR PRIMARY KEY,
                rule_name VARCHAR,
                hospital_code VARCHAR,
                status VARCHAR,
                total_count INTEGER,
                processed_count INTEGER,
                violation_count INTEGER,
                created_at TIMESTAMP,
                completed_at TIMESTAMP,
                error_message VARCHAR
            )
            """;

        using var command = connection.CreateCommand();
        command.CommandText = createTableSql;
        var dbCommand = (DbCommand)command;
        await dbCommand.ExecuteNonQueryAsync(cancellationToken);

        const string createIndexSql = """
            CREATE INDEX IF NOT EXISTS idx_audit_tasks_rule_name ON audit_tasks(rule_name);
            CREATE INDEX IF NOT EXISTS idx_audit_tasks_status ON audit_tasks(status);
            """;

        using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = createIndexSql;
        var dbIndexCommand = (DbCommand)indexCommand;
        await dbIndexCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 将 DTO 映射为领域模型（处理 status 字符串到枚举的转换）
    /// </summary>
    private static AuditTask MapToAuditTask(AuditTaskDto dto)
    {
        return new AuditTask
        {
            TaskId = dto.TaskId,
            RuleName = dto.RuleName,
            HospitalCode = dto.HospitalCode,
            Status = Enum.TryParse<TaskStatus>(dto.Status, ignoreCase: true, out var s)
                ? s
                : TaskStatus.Pending,
            TotalCount = dto.TotalCount,
            ProcessedCount = dto.ProcessedCount,
            ViolationCount = dto.ViolationCount,
            CreatedAt = dto.CreatedAt,
            CompletedAt = dto.CompletedAt,
            ErrorMessage = dto.ErrorMessage
        };
    }

    /// <summary>
    /// DuckDB 行映射 DTO，用于反序列化时处理 status 字段为字符串
    /// </summary>
    private sealed class AuditTaskDto
    {
        public string TaskId { get; set; } = string.Empty;
        public string RuleName { get; set; } = string.Empty;
        public string? HospitalCode { get; set; }
        public string Status { get; set; } = string.Empty;
        public int TotalCount { get; set; }
        public int ProcessedCount { get; set; }
        public int ViolationCount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? CompletedAt { get; set; }
        public string? ErrorMessage { get; set; }
    }
}
