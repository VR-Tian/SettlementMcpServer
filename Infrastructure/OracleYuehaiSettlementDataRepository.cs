using Dapper;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SettlementMcpServer.Infrastructure;

/// <summary>
/// 基于 Oracle + Dapper 的粤海医保结算数据仓储实现
/// </summary>
public sealed class OracleYuehaiSettlementDataRepository : IYuehaiSettlementDataRepository
{
    private const string TableName = "YB_粤海医保结算全量数据";
    private const int QueryTimeoutSeconds = 30;

    private readonly IYuehaiSettlementConnectionFactory _connectionFactory;
    private readonly ILogger<OracleYuehaiSettlementDataRepository> _logger;

    public OracleYuehaiSettlementDataRepository(
        IYuehaiSettlementConnectionFactory connectionFactory,
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

        _logger.LogTrace("执行结算数据查询");

        var results = await connection.QueryAsync<YuehaiSettlement>(commandDefinition);
        var resultList = results.ToList();

        _logger.LogTrace("结算数据查询完成，返回 {Count} 条记录", resultList.Count);

        return resultList;
    }

    private static (string sql, DynamicParameters parameters) BuildQuery(YuehaiSettlementQueryFilter filter)
    {
        var conditions = new List<string>();
        var parameters = new DynamicParameters();

        AddCondition(filter.VisitId, "就诊ID", "visitId", conditions, parameters);
        AddCondition(filter.SettlementId, "结算ID", "settlementId", conditions, parameters);
        AddCondition(filter.PersonnelNo, "人员编号", "personnelNo", conditions, parameters);
        AddCondition(filter.MedicalRecordNo, "病历号", "medicalRecordNo", conditions, parameters);
        AddCondition(filter.InpatientOutpatientNo, "住院_门诊号", "inpatientOutpatientNo", conditions, parameters);
        AddCondition(filter.InsuranceType, "险种类型1", "insuranceType", conditions, parameters);
        AddCondition(filter.MedicalCategory, "医疗类别", "medicalCategory", conditions, parameters);

        var whereClause = conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;

        var sql = $"SELECT * FROM {TableName} t {whereClause}";

        return (sql, parameters);
    }

    private static void AddCondition(
        string? value,
        string columnName,
        string paramName,
        List<string> conditions,
        DynamicParameters parameters)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            conditions.Add($"{columnName} = :{paramName}");
            parameters.Add(paramName, value);
        }
    }
}
