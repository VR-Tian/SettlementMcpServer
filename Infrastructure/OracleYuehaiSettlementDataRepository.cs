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
/// 该类继承 <see cref="OracleRepositoryBase{T}"/> 并实现 <see cref="IYuehaiSettlementDataRepository"/> 接口，
/// 使用 Dapper ORM 执行 SQL 查询并将结果集映射到 <see cref="YuehaiSettlement"/> 对象列表。
/// </para>
/// <para>
/// <b>连接工厂注入方式：</b>
/// 通过 <c>[FromKeyedServices("yuehai")]</c> 注入，使用 .NET 8+ Keyed Services 机制
/// 区分不同数据源的连接工厂，避免 DI 注册覆盖问题。
/// </para>
/// </remarks>
public sealed class OracleYuehaiSettlementDataRepository : OracleRepositoryBase<YuehaiSettlement>, IYuehaiSettlementDataRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<OracleYuehaiSettlementDataRepository> _logger;

    /// <summary>
    /// 初始化仓储实例
    /// </summary>
    /// <param name="connectionFactory">医保结算数据库连接工厂（通过 Keyed Services "yuehai" 注入）</param>
    /// <param name="logger">日志记录器（由 DI 注入，可选）</param>
    public OracleYuehaiSettlementDataRepository(
        [FromKeyedServices("yuehai")] IDbConnectionFactory connectionFactory,
        ILogger<OracleYuehaiSettlementDataRepository>? logger = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? NullLogger<OracleYuehaiSettlementDataRepository>.Instance;
    }

    /// <inheritdoc />
    protected override string TableName => "YB_YueHai医保结算全量数据";

    /// <inheritdoc />
    protected override void AddFilterConditions(
        DynamicParameters parameters,
        List<string> conditions,
        object filter)
    {
        if (filter is not YuehaiSettlementQueryFilter yuehaiFilter)
        {
            return;
        }

        SqlWhereBuilder.AddCondition(yuehaiFilter.VisitId, "就诊ID", "visitId", conditions, parameters);
        SqlWhereBuilder.AddCondition(yuehaiFilter.SettlementId, "结算ID", "settlementId", conditions, parameters);
        SqlWhereBuilder.AddCondition(yuehaiFilter.PersonnelNo, "人员编号", "personnelNo", conditions, parameters);
        SqlWhereBuilder.AddCondition(yuehaiFilter.MedicalRecordNo, "病历号", "medicalRecordNo", conditions, parameters);
        SqlWhereBuilder.AddCondition(yuehaiFilter.InpatientOutpatientNo, "住院_门诊号", "inpatientOutpatientNo", conditions, parameters);
        SqlWhereBuilder.AddCondition(yuehaiFilter.InsuranceType, "险种类型1", "insuranceType", conditions, parameters);
        SqlWhereBuilder.AddCondition(yuehaiFilter.MedicalCategory, "医疗类别", "medicalCategory", conditions, parameters);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<YuehaiSettlement>> QueryAllSettlementsAsync(
        YuehaiSettlementQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteFullQueryAsync(_connectionFactory, filter, _logger, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<int> CountSettlementsAsync(
        YuehaiSettlementQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await ExecuteCountQueryAsync(_connectionFactory, filter, _logger, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<YuehaiSettlement>> QuerySettlementsAsync(
        YuehaiSettlementQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        return await ExecutePaginatedQueryAsync(_connectionFactory, filter, _logger, cancellationToken);
    }
}
