using System.Data;
using System.Data.Common;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;

namespace SettlementMcpServer.Infrastructure.DuckDb;

/// <summary>
/// DuckDB 审核结果仓储实现
/// </summary>
/// <remarks>
/// <para>
/// 负责将审核结果持久化到 DuckDB 数据库，支持批量写入和按任务ID、规则编码查询。
/// </para>
/// <para>
/// 使用 <see cref="IDbConnectionFactory"/> 创建数据库连接，
/// 通过 <c>[FromKeyedServices("duckdb")]</c> 注入 DuckDB 连接工厂。
/// </para>
/// </remarks>
public sealed class DuckDbAuditResultRepository : IAuditResultRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<DuckDbAuditResultRepository> _logger;

    /// <summary>
    /// 初始化 DuckDB 审核结果仓储
    /// </summary>
    /// <param name="connectionFactory">DuckDB 连接工厂</param>
    /// <param name="logger">日志记录器</param>
    public DuckDbAuditResultRepository(
        [FromKeyedServices("duckdb")] IDbConnectionFactory connectionFactory,
        ILogger<DuckDbAuditResultRepository>? logger = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? NullLogger<DuckDbAuditResultRepository>.Instance;
    }

    /// <inheritdoc />
    public async Task SaveAuditResultsAsync(IEnumerable<AuditResult> results, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(results);

        var resultList = results.ToList();
        if (resultList.Count == 0)
        {
            _logger.LogDebug("没有审核结果需要保存");
            return;
        }

        _logger.LogInformation("开始保存 {Count} 条审核结果到 DuckDB", resultList.Count);

        var connection = _connectionFactory.CreateConnection();

        // 确保表存在
        await EnsureTableExistsAsync(connection, cancellationToken);

        // 使用事务批量写入
        using var transaction = connection.BeginTransaction();
        try
        {
            const string insertSql = """
                INSERT INTO audit_results (
                    id, task_id, rule_name, rule_type,
                    personnel_no, personnel_name,
                    institution_code, institution_name,
                    violation_item_code, violation_item_name,
                    violation_quantity, violation_unit_price, violation_amount,
                    fee_occurrence_date, audit_time,
                    receiving_dept_code, receiving_dept_name,
                    prompt_message, status, handle_remark,
                    created_at
                ) VALUES (
                    @Id, @TaskId, @RuleName, @RuleType,
                    @PersonnelNo, @PersonnelName,
                    @InstitutionCode, @InstitutionName,
                    @ViolationItemCode, @ViolationItemName,
                    @ViolationQuantity, @ViolationUnitPrice, @ViolationAmount,
                    @FeeOccurrenceDate, @AuditTime,
                    @ReceivingDeptCode, @ReceivingDeptName,
                    @PromptMessage, @Status, @HandleRemark,
                    @CreatedAt
                )
                """;

            await connection.ExecuteAsync(insertSql, resultList, transaction: transaction, commandType: CommandType.Text);

            transaction.Commit();

            _logger.LogInformation("成功保存 {Count} 条审核结果到 DuckDB", resultList.Count);
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "保存审核结果到 DuckDB 时发生异常");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditResult>> GetAuditResultsByTaskIdAsync(string taskId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(taskId);

        _logger.LogDebug("根据任务ID查询审核结果: {TaskId}", taskId);

        var connection = _connectionFactory.CreateConnection();

        await EnsureTableExistsAsync(connection, cancellationToken);

        const string sql = "SELECT * FROM audit_results WHERE task_id = @TaskId ORDER BY created_at DESC";
        var results = await connection.QueryAsync<AuditResult>(sql, new { TaskId = taskId });

        var resultList = results.ToList();
        _logger.LogDebug("根据任务ID {TaskId} 查询到 {Count} 条审核结果", taskId, resultList.Count);

        return resultList;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AuditResult>> GetAuditResultsByRuleNameAsync(string ruleName, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ruleName);

        _logger.LogDebug("根据规则名称查询审核结果: {RuleName}", ruleName);

        var connection = _connectionFactory.CreateConnection();

        await EnsureTableExistsAsync(connection, cancellationToken);

        const string sql = "SELECT * FROM audit_results WHERE rule_name = @RuleName ORDER BY created_at DESC";
        var results = await connection.QueryAsync<AuditResult>(sql, new { RuleName = ruleName });

        var resultList = results.ToList();
        _logger.LogDebug("根据规则名称 {RuleName} 查询到 {Count} 条审核结果", ruleName, resultList.Count);

        return resultList;
    }

    /// <summary>
    /// 确保 audit_results 表存在
    /// </summary>
    private async Task EnsureTableExistsAsync(IDbConnection connection, CancellationToken cancellationToken)
    {
        const string createTableSql = """
            CREATE TABLE IF NOT EXISTS audit_results (
                id VARCHAR PRIMARY KEY,
                task_id VARCHAR,
                rule_name VARCHAR,
                rule_type VARCHAR,
                personnel_no VARCHAR,
                personnel_name VARCHAR,
                institution_code VARCHAR,
                institution_name VARCHAR,
                violation_item_code VARCHAR,
                violation_item_name VARCHAR,
                violation_quantity DECIMAL,
                violation_unit_price DECIMAL,
                violation_amount DECIMAL,
                fee_occurrence_date VARCHAR,
                audit_time TIMESTAMP,
                receiving_dept_code VARCHAR,
                receiving_dept_name VARCHAR,
                prompt_message VARCHAR,
                status VARCHAR,
                handle_remark VARCHAR,
                created_at TIMESTAMP
            )
            """;

        using var command = connection.CreateCommand();
        command.CommandText = createTableSql;
        var dbCommand = (DbCommand)command;
        await dbCommand.ExecuteNonQueryAsync(cancellationToken);

        // 创建索引（使用 IF NOT EXISTS 避免重复创建）
        const string createIndexesSql = """
            CREATE INDEX IF NOT EXISTS idx_audit_results_task_id ON audit_results(task_id);
            CREATE INDEX IF NOT EXISTS idx_audit_results_rule_name ON audit_results(rule_name);
            CREATE INDEX IF NOT EXISTS idx_audit_results_personnel_no ON audit_results(personnel_no);
            CREATE INDEX IF NOT EXISTS idx_audit_results_institution_code ON audit_results(institution_code);
            """;

        using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = createIndexesSql;
        var dbIndexCommand = (DbCommand)indexCommand;
        await dbIndexCommand.ExecuteNonQueryAsync(cancellationToken);
    }
}
