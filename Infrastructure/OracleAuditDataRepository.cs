using Dapper;
using Microsoft.Extensions.DependencyInjection;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SettlementMcpServer.Infrastructure;

/// <summary>
/// 基于 Oracle + Dapper 的审核数据仓储实现
/// </summary>
/// <remarks>
/// <para>
/// 该类继承 <see cref="OracleRepositoryBase{T}"/> 并实现 <see cref="IAuditDataRepository"/> 接口，
/// 使用 Dapper ORM 执行 SQL 查询并将结果集映射到 <see cref="AuditedResult"/> 对象列表。
/// </para>
/// <para>
/// <b>连接工厂注入方式：</b>
/// 通过 <c>[FromKeyedServices("audit")]</c> 注入，使用 .NET 8+ Keyed Services 机制
/// 区分不同数据源的连接工厂，避免 DI 注册覆盖问题。
/// </para>
/// </remarks>
public sealed class OracleAuditDataRepository : OracleRepositoryBase<AuditedResult>, IAuditDataRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<OracleAuditDataRepository> _logger;

    /// <summary>
    /// 初始化仓储实例
    /// </summary>
    /// <param name="connectionFactory">审核数据库连接工厂（通过 Keyed Services "audit" 注入）</param>
    /// <param name="logger">日志记录器（由 DI 注入，可选）</param>
    public OracleAuditDataRepository(
        [FromKeyedServices("audit")] IDbConnectionFactory connectionFactory,
        ILogger<OracleAuditDataRepository>? logger = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? NullLogger<OracleAuditDataRepository>.Instance;
    }

    /// <inheritdoc />
    protected override string TableName => "DW_AUDITED_RESULT_1464_25_WEIMIN";

    /// <inheritdoc />
    protected override void AddFilterConditions(
        DynamicParameters parameters,
        List<string> conditions,
        object filter)
    {
        if (filter is not AuditedResultQueryFilter auditFilter)
        {
            throw new System.ArgumentException($"Filter must be of type {nameof(AuditedResultQueryFilter)}", nameof(filter));
        }

        SqlWhereBuilder.AddCondition(auditFilter.MedicalRecordNo, "病案号", "medicalRecordNo", conditions, parameters);
        SqlWhereBuilder.AddCondition(auditFilter.HospitalCode, "医院编码", "hospitalCode", conditions, parameters);
        SqlWhereBuilder.AddCondition(auditFilter.InsuredNo, "参保人号", "insuredNo", conditions, parameters);
        SqlWhereBuilder.AddCondition(auditFilter.RuleName, "规则名称", "ruleName", conditions, parameters);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// 此方法使用 Oracle 的 ROWNUM 嵌套查询实现分页：
    /// </para>
    /// <code>
    /// SELECT * FROM (
    ///     SELECT t.*, ROWNUM as rn FROM 表名 t WHERE ...
    /// ) WHERE rn &gt;= :startRow AND rn &lt;= :endRow
    /// </code>
    /// <para>
    /// 其中 <c>startRow = (Page - 1) * PageSize + 1</c>，<c>endRow = Page * PageSize</c>。
    /// 这种分页方式确保 WHERE 过滤后按行号截取，而非取前 N 行。
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<AuditedResult>> QueryAuditedResultsAsync(
        AuditedResultQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await ExecutePaginatedQueryAsync(_connectionFactory, filter, _logger, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// 此方法执行 <c>SELECT COUNT(*)</c> 查询，使用与 <see cref="QueryAuditedResultsAsync"/> 相同的 WHERE 条件，
    /// 但忽略分页参数（Page/PageSize），返回符合过滤条件的总记录数。
    /// </para>
    /// </remarks>
    public async Task<int> CountAuditedResultsAsync(
        AuditedResultQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteCountQueryAsync(_connectionFactory, filter, _logger, cancellationToken);
    }

    /// <inheritdoc />
    /// <remarks>
    /// <para>
    /// 此方法执行不分页的全量查询，直接返回符合过滤条件的全部数据。
    /// 用于 Excel 导出等需要一次性获取全部数据的场景。
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyList<AuditedResult>> QueryAllAuditedResultsAsync(
        AuditedResultQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteFullQueryAsync(_connectionFactory, filter, _logger, cancellationToken);
    }
}
