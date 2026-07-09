using Dapper;
using Microsoft.Extensions.DependencyInjection;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace SettlementMcpServer.Infrastructure;

/// <summary>
/// 基于 Oracle + Dapper 的医保结算医保结算数据仓储实现
/// </summary>
/// <remarks>
/// <para>
/// 该类继承 <see cref="OracleRepositoryBase{T}"/> 并实现 <see cref="ISettlementDataRepository"/> 接口，
/// 使用 Dapper ORM 执行 SQL 查询并将结果集映射到 <see cref="Settlement"/> 对象列表。
/// </para>
/// <para>
/// <b>连接工厂注入方式：</b>
/// 通过 <c>[FromKeyedServices("")]</c> 注入，使用 .NET 8+ Keyed Services 机制
/// 区分不同数据源的连接工厂，避免 DI 注册覆盖问题。
/// </para>
/// </remarks>
public sealed class OracleSettlementDataRepository : OracleRepositoryBase<Settlement>, ISettlementDataRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<OracleSettlementDataRepository> _logger;

    /// <summary>
    /// 初始化仓储实例
    /// </summary>
    /// <param name="connectionFactory">医保结算数据库连接工厂（通过 Keyed Services "" 注入）</param>
    /// <param name="logger">日志记录器（由 DI 注入，可选）</param>
    public OracleSettlementDataRepository(
        [FromKeyedServices("")] IDbConnectionFactory connectionFactory,
        ILogger<OracleSettlementDataRepository>? logger = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? NullLogger<OracleSettlementDataRepository>.Instance;
    }

    /// <inheritdoc />
    protected override string TableName => "督导_H44011200048";

    /// <inheritdoc />
    protected override void AddFilterConditions(
        DynamicParameters parameters,
        List<string> conditions,
        object filter)
    {
        if (filter is not SettlementQueryFilter Filter)
        {
            return;
        }

        SqlWhereBuilder.AddCondition(Filter.VisitId, "就诊ID", "visitId", conditions, parameters);
        SqlWhereBuilder.AddCondition(Filter.SettlementId, "结算ID", "settlementId", conditions, parameters);
        SqlWhereBuilder.AddCondition(Filter.PersonnelNo, "人员编号", "personnelNo", conditions, parameters);
        SqlWhereBuilder.AddCondition(Filter.MedicalRecordNo, "病历号", "medicalRecordNo", conditions, parameters);
        SqlWhereBuilder.AddCondition(Filter.InpatientOutpatientNo, "住院_门诊号", "inpatientOutpatientNo", conditions, parameters);
        SqlWhereBuilder.AddCondition(Filter.InsuranceType, "险种类型1", "insuranceType", conditions, parameters);
        SqlWhereBuilder.AddCondition(Filter.MedicalCategory, "医疗类别", "medicalCategory", conditions, parameters);
        SqlWhereBuilder.AddCondition(Filter.InstitutionCode, "定点医药机构编号", "institutionCode", conditions, parameters);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Settlement>> QueryAllSettlementsAsync(
        SettlementQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        // 诊断：查询表的实际列名
        using var connection = _connectionFactory.CreateConnection();
        var columnSql = $"SELECT column_name FROM user_tab_columns WHERE table_name = '{TableName}' ORDER BY column_id";
        var columns = await connection.QueryAsync<string>(columnSql);
        _logger.LogDebug("诊断 - Oracle 表 {TableName} 的实际列名：{Columns}", TableName, string.Join(", ", columns));
        
        var results = await ExecuteFullQueryAsync(_connectionFactory, filter, _logger, cancellationToken);
        
        // 诊断：检查前 5 条数据的 InstitutionCode 值
        if (results.Count > 0)
        {
            var sampleCodes = results.Take(5).Select(r => r.InstitutionCode ?? "NULL").ToList();
            _logger.LogDebug("诊断 - Oracle 查询结果 InstitutionCode 样本值：{SampleCodes}", string.Join(", ", sampleCodes));
        }
        
        return results;
    }

    /// <inheritdoc />
    public async Task<int> CountSettlementsAsync(
        SettlementQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteCountQueryAsync(_connectionFactory, filter, _logger, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Settlement>> QuerySettlementsAsync(
        SettlementQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await ExecutePaginatedQueryAsync(_connectionFactory, filter, _logger, cancellationToken);
    }
}
