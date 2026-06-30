using Dapper;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SettlementMcpServer.Infrastructure;

/// <summary>
/// 基于 Oracle + Dapper 的YueHai医保结算数据仓储实现
/// </summary>
public sealed class OracleYuehaiSettlementDataRepository : IYuehaiSettlementDataRepository
{
    private const string TableName = "YB_YueHai医保结算全量数据";
    private const int QueryTimeoutSeconds = 30;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<OracleYuehaiSettlementDataRepository> _logger;

    public OracleYuehaiSettlementDataRepository(
        IDbConnectionFactory connectionFactory,
        ILogger<OracleYuehaiSettlementDataRepository>? logger = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? NullLogger<OracleYuehaiSettlementDataRepository>.Instance;
    }

    public async Task<IReadOnlyList<YuehaiSettlement>> QueryAllSettlementsAsync(
        YuehaiSettlementQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var (sql, parameters) = BuildQuery(filter);

        using var connection = _connectionFactory.CreateConnection();

        var commandDefinition = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            commandTimeout: QueryTimeoutSeconds,
            cancellationToken: cancellationToken);

        _logger.LogDebug("执行结算数据全量查询: Sql={Sql}", sql);

        var results = await connection.QueryAsync<YuehaiSettlement>(commandDefinition);
        var resultList = results.ToList();

        _logger.LogDebug("结算数据全量查询完成，返回 {Count} 条记录", resultList.Count);

        return resultList;
    }

    public async Task<int> CountSettlementsAsync(
        YuehaiSettlementQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var (sql, parameters) = BuildCountQuery(filter);

        using var connection = _connectionFactory.CreateConnection();

        var commandDefinition = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            commandTimeout: QueryTimeoutSeconds,
            cancellationToken: cancellationToken);

        _logger.LogDebug("执行结算数据计数查询: Sql={Sql}", sql);

        var count = await connection.ExecuteScalarAsync<int>(commandDefinition);

        _logger.LogDebug("结算数据计数查询完成，总计 {Count} 条记录", count);

        return count;
    }

    public async Task<IReadOnlyList<YuehaiSettlement>> QuerySettlementsAsync(
        YuehaiSettlementQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        var (sql, parameters) = BuildPaginatedQuery(filter);

        using var connection = _connectionFactory.CreateConnection();

        var commandDefinition = new CommandDefinition(
            commandText: sql,
            parameters: parameters,
            commandTimeout: QueryTimeoutSeconds,
            cancellationToken: cancellationToken);

        _logger.LogDebug("执行结算数据分页查询: Page={Page}, PageSize={PageSize}, Sql={Sql}", filter.Page, filter.PageSize, sql);

        var results = await connection.QueryAsync<YuehaiSettlement>(commandDefinition);
        var resultList = results.ToList();

        _logger.LogDebug("结算数据分页查询完成，返回 {Count} 条记录", resultList.Count);

        return resultList;
    }

    private static (string sql, DynamicParameters parameters) BuildQuery(YuehaiSettlementQueryFilter filter)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        AddCommonConditions(filter, conditions, parameters);

        var whereClause = SqlWhereBuilder.BuildWhereClause(conditions);

        var sql = $"SELECT * FROM {TableName} t {whereClause}";

        return (sql, parameters);
    }

    private static (string sql, DynamicParameters parameters) BuildCountQuery(YuehaiSettlementQueryFilter filter)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        AddCommonConditions(filter, conditions, parameters);

        var whereClause = SqlWhereBuilder.BuildWhereClause(conditions);

        var sql = $"SELECT COUNT(*) FROM {TableName} t {whereClause}";

        return (sql, parameters);
    }

    private static (string sql, DynamicParameters parameters) BuildPaginatedQuery(YuehaiSettlementQueryFilter filter)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        AddCommonConditions(filter, conditions, parameters);

        var whereClause = SqlWhereBuilder.BuildWhereClause(conditions);

        var page = Math.Max(filter.Page, 1);
        var pageSize = Math.Clamp(filter.PageSize, 1, 500);
        var startRow = (page - 1) * pageSize + 1;
        var endRow = page * pageSize;

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

    private static void AddCommonConditions(
        YuehaiSettlementQueryFilter filter,
        List<string> conditions,
        DynamicParameters parameters)
    {
        SqlWhereBuilder.AddCondition(filter.VisitId, "就诊ID", "visitId", conditions, parameters);
        SqlWhereBuilder.AddCondition(filter.SettlementId, "结算ID", "settlementId", conditions, parameters);
        SqlWhereBuilder.AddCondition(filter.PersonnelNo, "人员编号", "personnelNo", conditions, parameters);
        SqlWhereBuilder.AddCondition(filter.MedicalRecordNo, "病历号", "medicalRecordNo", conditions, parameters);
        SqlWhereBuilder.AddCondition(filter.InpatientOutpatientNo, "住院_门诊号", "inpatientOutpatientNo", conditions, parameters);
        SqlWhereBuilder.AddCondition(filter.InsuranceType, "险种类型1", "insuranceType", conditions, parameters);
        SqlWhereBuilder.AddCondition(filter.MedicalCategory, "医疗类别", "medicalCategory", conditions, parameters);
    }
}
