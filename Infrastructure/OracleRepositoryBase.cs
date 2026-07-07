using Dapper;
using SettlementMcpServer.Contracts;
using Microsoft.Extensions.Logging;

namespace SettlementMcpServer.Infrastructure;

/// <summary>
/// Oracle 仓储泛型基类，封装通用的 SQL 执行逻辑
/// </summary>
/// <typeparam name="T">数据模型类型</typeparam>
/// <remarks>
/// <para>
/// 使用模板方法模式，基类定义算法骨架（连接管理、SQL 构建、日志记录），
/// 子类实现具体步骤（表名、过滤条件）。
/// </para>
/// <para>
/// 设计要点：
/// <list type="bullet">
///   <item><description>连接管理：使用 <c>using</c> 语句自动管理连接生命周期</description></item>
///   <item><description>CommandDefinition：统一构建命令定义，包含超时和取消令牌</description></item>
///   <item><description>分页查询：使用 Oracle ROWNUM 分页模板</description></item>
///   <item><description>日志记录：统一输出 SQL 和结果计数</description></item>
/// </list>
/// </para>
/// </remarks>
public abstract class OracleRepositoryBase<T> where T : class
{
    /// <summary>
    /// 默认每页条数
    /// </summary>
    protected const int DefaultPageSize = 100;

    /// <summary>
    /// 每页最大条数，防止单次查询过大导致上下文溢出
    /// </summary>
    protected const int MaxPageSize = 500;

    /// <summary>
    /// 查询超时时间（秒），防止慢查询无限期阻塞
    /// </summary>
    protected const int QueryTimeoutSeconds = 30;

    /// <summary>
    /// 返回目标表名
    /// </summary>
    protected abstract string TableName { get; }

    /// <summary>
    /// 添加过滤条件到 WHERE 子句和参数集合
    /// </summary>
    /// <param name="parameters">Dapper 动态参数集合</param>
    /// <param name="conditions">WHERE 条件集合</param>
    /// <param name="filter">查询过滤器对象</param>
    protected abstract void AddFilterConditions(
        DynamicParameters parameters,
        List<string> conditions,
        object filter);

    /// <summary>
    /// 执行分页查询
    /// </summary>
    /// <typeparam name="TFilter">过滤器类型，必须实现 <see cref="IPagedQuery"/></typeparam>
    /// <param name="connectionFactory">数据库连接工厂</param>
    /// <param name="filter">查询过滤器</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前页的数据列表</returns>
    protected async Task<IReadOnlyList<T>> ExecutePaginatedQueryAsync<TFilter>(
        IDbConnectionFactory connectionFactory,
        TFilter filter,
        ILogger logger,
        CancellationToken cancellationToken)
        where TFilter : IPagedQuery
    {
        ArgumentNullException.ThrowIfNull(filter);

        // 规范化分页参数
        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, MaxPageSize);
        var startRow = (page - 1) * pageSize + 1;
        var endRow = page * pageSize;

        // 构建分页查询 SQL 和参数
        var (sql, parameters) = BuildPaginatedQuerySql(filter, startRow, endRow);

        using var connection = connectionFactory.CreateConnection();

        var commandDefinition = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            commandTimeout: QueryTimeoutSeconds,
            cancellationToken: cancellationToken);

        logger.LogDebug("执行分页查询: Page={Page}, PageSize={PageSize}, StartRow={StartRow}, EndRow={EndRow}, Sql={Sql}",
            page, pageSize, startRow, endRow, sql);

        var results = await connection.QueryAsync<T>(commandDefinition);
        var resultList = results.ToList();

        logger.LogDebug("分页查询完成，返回 {Count} 条记录", resultList.Count);

        return resultList;
    }

    /// <summary>
    /// 执行全量查询（不分页）
    /// </summary>
    /// <typeparam name="TFilter">过滤器类型</typeparam>
    /// <param name="connectionFactory">数据库连接工厂</param>
    /// <param name="filter">查询过滤器</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>全部符合条件的数据列表</returns>
    protected async Task<IReadOnlyList<T>> ExecuteFullQueryAsync<TFilter>(
        IDbConnectionFactory connectionFactory,
        TFilter filter,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        // 构建全量查询 SQL 和参数
        var (sql, parameters) = BuildFullQuerySql(filter);

        using var connection = connectionFactory.CreateConnection();

        var commandDefinition = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            commandTimeout: QueryTimeoutSeconds,
            cancellationToken: cancellationToken);

        logger.LogDebug("执行数据连接地址: {ConnectionString}", connection.ConnectionString);

        logger.LogDebug("执行全量查询: Sql={Sql}", sql);

        var results = await connection.QueryAsync<T>(commandDefinition);
        var resultList = results.ToList();

        logger.LogDebug("全量查询完成，返回 {Count} 条记录", resultList.Count);

        return resultList;
    }

    /// <summary>
    /// 执行计数查询
    /// </summary>
    /// <typeparam name="TFilter">过滤器类型</typeparam>
    /// <param name="connectionFactory">数据库连接工厂</param>
    /// <param name="filter">查询过滤器</param>
    /// <param name="logger">日志记录器</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>符合条件的记录总数</returns>
    protected async Task<int> ExecuteCountQueryAsync<TFilter>(
        IDbConnectionFactory connectionFactory,
        TFilter filter,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(filter);

        // 构建计数查询 SQL 和参数
        var (sql, parameters) = BuildCountQuerySql(filter);

        using var connection = connectionFactory.CreateConnection();

        var commandDefinition = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            commandTimeout: QueryTimeoutSeconds,
            cancellationToken: cancellationToken);

        logger.LogDebug("执行计数查询: Sql={Sql}", sql);

        var totalCount = await connection.ExecuteScalarAsync<int>(commandDefinition);

        logger.LogDebug("计数查询完成，总计 {Count} 条记录", totalCount);

        return totalCount;
    }

    /// <summary>
    /// 构建分页查询 SQL 语句
    /// </summary>
    /// <typeparam name="TFilter">过滤器类型</typeparam>
    /// <param name="filter">查询过滤器</param>
    /// <param name="startRow">起始行号</param>
    /// <param name="endRow">结束行号</param>
    /// <returns>SQL 文本和参数集合</returns>
    private (string sql, DynamicParameters parameters) BuildPaginatedQuerySql<TFilter>(
        TFilter filter,
        int startRow,
        int endRow)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        AddFilterConditions(parameters, conditions, filter);

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

    /// <summary>
    /// 构建全量查询 SQL 语句
    /// </summary>
    /// <typeparam name="TFilter">过滤器类型</typeparam>
    /// <param name="filter">查询过滤器</param>
    /// <returns>SQL 文本和参数集合</returns>
    private (string sql, DynamicParameters parameters) BuildFullQuerySql<TFilter>(TFilter filter)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        AddFilterConditions(parameters, conditions, filter);

        var whereClause = SqlWhereBuilder.BuildWhereClause(conditions);

        var sql = $"SELECT * FROM {TableName} t {whereClause}";

        return (sql, parameters);
    }

    /// <summary>
    /// 构建计数查询 SQL 语句
    /// </summary>
    /// <typeparam name="TFilter">过滤器类型</typeparam>
    /// <param name="filter">查询过滤器</param>
    /// <returns>SQL 文本和参数集合</returns>
    private (string sql, DynamicParameters parameters) BuildCountQuerySql<TFilter>(TFilter filter)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        AddFilterConditions(parameters, conditions, filter);

        var whereClause = SqlWhereBuilder.BuildWhereClause(conditions);

        var sql = $"SELECT COUNT(*) FROM {TableName} t {whereClause}";

        return (sql, parameters);
    }
}
