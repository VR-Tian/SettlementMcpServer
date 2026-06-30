using Dapper;
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
/// 该类实现 <see cref="IAuditDataRepository"/> 接口，使用 Dapper ORM 执行 SQL 查询并将结果集映射到
/// <see cref="AuditedResult"/> 对象列表。
/// </para>
/// <para>
/// <b>Dapper 最佳实践要点：</b>
/// <list type="number">
///   <item>
///     <b>连接工厂注入</b>：通过 <see cref="IDbConnectionFactory"/> 获取连接，
///     仓储不依赖具体数据库驱动类型。
///   </item>
///   <item>
///     <b>CommandDefinition</b>：包装 SQL + 参数 + 超时 + 取消令牌，是 Dapper 推荐的异步调用方式。
///   </item>
///   <item>
///     <b>await using</b>：自动管理连接生命周期，连接关闭后自动归还连接池。
///   </item>
///   <item>
///     <b>IL 发射缓存</b>：Dapper 缓存类型映射 IL 代码，首次映射后有极小开销，
///     后续查询性能接近原生 ADO.NET。
///   </item>
///   <item>
///     <b>参数化查询</b>：所有用户输入通过 <see cref="DynamicParameters"/> 传递，
///     数据库驱动自动处理 SQL 注入防护。
///   </item>
/// </list>
/// </para>
/// </remarks>
public sealed class OracleAuditDataRepository : IAuditDataRepository
{
    /// <summary>
    /// 目标表名称
    /// </summary>
    private const string TableName = "DW_AUDITED_RESULT_1464_24AND25";

    /// <summary>
    /// 默认每页条数
    /// </summary>
    private const int DefaultPageSize = 100;

    /// <summary>
    /// 每页最大条数，防止单次查询过大导致上下文溢出
    /// </summary>
    private const int MaxPageSize = 500;

    /// <summary>
    /// 查询超时时间（秒），防止慢查询无限期阻塞
    /// </summary>
    private const int QueryTimeoutSeconds = 30;

    private readonly IAuditDbConnectionFactory _connectionFactory;
    private readonly ILogger<OracleAuditDataRepository> _logger;

    /// <summary>
    /// 初始化仓储实例
    /// </summary>
    /// <param name="connectionFactory">审核数据库连接工厂（由 DI 注入）</param>
    /// <param name="logger">日志记录器（由 DI 注入，可选）</param>
    public OracleAuditDataRepository(
        IAuditDbConnectionFactory connectionFactory,
        ILogger<OracleAuditDataRepository>? logger = null)
    {
        _connectionFactory = connectionFactory ?? throw new System.ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? NullLogger<OracleAuditDataRepository>.Instance;
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
        System.ArgumentNullException.ThrowIfNull(filter);

        // 规范化分页参数
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, MaxPageSize);
        var startRow = (page - 1) * pageSize + 1;
        var endRow = page * pageSize;

        // 构建动态 SQL 和参数集合（包含分页参数）
        var (sql, parameters) = BuildPaginatedQuery(filter, startRow, endRow);

        // 通过工厂创建连接（内部使用连接池复用物理连接）
        using var connection = _connectionFactory.CreateConnection();



        // 构建 Dapper 命令定义（包含超时和取消令牌）
        var commandDefinition = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            commandTimeout: QueryTimeoutSeconds,
            cancellationToken: cancellationToken);

        _logger.LogDebug("执行审核数据分页查询: Page={Page}, PageSize={PageSize}, StartRow={StartRow}, EndRow={EndRow}, Sql={Sql}",
            page, pageSize, startRow, endRow, sql);

        var results = await connection.QueryAsync<AuditedResult>(commandDefinition);
        var resultList = results.ToList();

        _logger.LogDebug("审核数据分页查询完成，返回 {Count} 条记录", resultList.Count);

        return resultList;
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
        System.ArgumentNullException.ThrowIfNull(filter);

        // 构建 COUNT SQL 和参数集合（不包含分页参数）
        var (sql, parameters) = BuildCountQuery(filter);

        using var connection = _connectionFactory.CreateConnection();

        var commandDefinition = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            commandTimeout: QueryTimeoutSeconds,
            cancellationToken: cancellationToken);

        _logger.LogDebug("执行审核数据计数查询: Sql={Sql}", sql);

        // ExecuteScalarAsync 返回结果集第一行第一列的值
        var totalCount = await connection.ExecuteScalarAsync<int>(commandDefinition);

        _logger.LogDebug("审核数据计数查询完成，总计 {Count} 条记录", totalCount);

        return totalCount;
    }

    /// <summary>
    /// 构建分页查询的 SQL 语句和参数集合
    /// </summary>
    /// <param name="filter">查询条件过滤器</param>
    /// <param name="startRow">起始行号（ROWNUM 下限）</param>
    /// <param name="endRow">结束行号（ROWNUM 上限）</param>
    /// <returns>SQL 文本和参数集合的元组</returns>
    /// <remarks>
    /// Oracle 分页需使用三层嵌套查询确保 ROWNUM 在 WHERE 过滤后计算：
    /// <code>
    /// SELECT * FROM (
    ///     SELECT t.*, ROWNUM as rn FROM 表名 t WHERE 条件
    /// ) WHERE rn &gt;= :startRow AND rn &lt;= :endRow
    /// </code>
    /// </remarks>
    private static (string sql, DynamicParameters parameters) BuildPaginatedQuery(
        AuditedResultQueryFilter filter,
        int startRow,
        int endRow)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        // 构建 WHERE 过滤条件
        AddCommonConditions(filter, conditions, parameters);

        var whereClause = SqlWhereBuilder.BuildWhereClause(conditions);

        var sql = $"""
            SELECT * FROM (
                SELECT t.*, ROWNUM as rn FROM {TableName} t
                {whereClause}
            ) WHERE rn >= :startRow AND rn <= :endRow
            """;

        parameters.Add("startRow", startRow);
        parameters.Add("endRow", endRow);

        return (sql, parameters);
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
        System.ArgumentNullException.ThrowIfNull(filter);

        // 构建不分页的 SQL 和参数集合
        var (sql, parameters) = BuildFullQuery(filter);

        using var connection = _connectionFactory.CreateConnection();

        var commandDefinition = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            commandTimeout: QueryTimeoutSeconds,
            cancellationToken: cancellationToken);

        _logger.LogDebug("执行审核数据源地址: {DataSource}", connection.ConnectionString);

        _logger.LogDebug("执行审核数据全量查询: Sql={Sql}", sql);

        var results = await connection.QueryAsync<AuditedResult>(commandDefinition);
        var resultList = results.ToList();

        _logger.LogDebug("审核数据全量查询完成，返回 {Count} 条记录", resultList.Count);

        return resultList;
    }

    /// <summary>
    /// 构建不分页的全量查询 SQL 语句和参数集合
    /// </summary>
    /// <param name="filter">查询条件过滤器</param>
    /// <returns>SQL 文本和参数集合的元组</returns>
    private static (string sql, DynamicParameters parameters) BuildFullQuery(AuditedResultQueryFilter filter)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        // 构建 WHERE 过滤条件
        AddCommonConditions(filter, conditions, parameters);

        var whereClause = SqlWhereBuilder.BuildWhereClause(conditions);

        var sql = $"SELECT * FROM {TableName} {whereClause}";

        return (sql, parameters);
    }

    /// <summary>
    /// 构建 COUNT 查询的 SQL 语句和参数集合
    /// </summary>
    /// <param name="filter">查询条件过滤器（仅使用过滤字段，忽略分页参数）</param>
    /// <returns>SQL 文本和参数集合的元组</returns>
    private static (string sql, DynamicParameters parameters) BuildCountQuery(AuditedResultQueryFilter filter)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        // 构建 WHERE 过滤条件（与分页查询相同）
        AddCommonConditions(filter, conditions, parameters);

        var whereClause = SqlWhereBuilder.BuildWhereClause(conditions);

        var sql = $"SELECT COUNT(*) FROM {TableName} {whereClause}";

        return (sql, parameters);
    }

    /// <summary>
    /// 添加通用过滤条件（病案号、医院编码、参保人号、规则编码）
    /// </summary>
    /// <param name="filter">查询条件过滤器</param>
    /// <param name="conditions">WHERE 条件集合</param>
    /// <param name="parameters">Dapper 动态参数集合</param>
    private static void AddCommonConditions(
        AuditedResultQueryFilter filter,
        List<string> conditions,
        DynamicParameters parameters)
    {
        SqlWhereBuilder.AddCondition(filter.MedicalRecordNo, "病案号", "medicalRecordNo", conditions, parameters);
        SqlWhereBuilder.AddCondition(filter.HospitalCode, "医院编码", "hospitalCode", conditions, parameters);
        SqlWhereBuilder.AddCondition(filter.InsuredNo, "参保人号", "insuredNo", conditions, parameters);
        SqlWhereBuilder.AddCondition(filter.RuleCode, "规则编码", "ruleCode", conditions, parameters);
    }
}
