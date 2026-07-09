using System.Data.Common;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using DuckDB.NET.Data;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;

namespace SettlementMcpServer.Infrastructure.DuckDb;

/// <summary>
/// 基于 DuckDB 的医保结算数据仓储实现
/// </summary>
/// <remarks>
/// <para>
/// 从 DuckDB 的 <c>_settlements</c> 视图查询结算数据，该视图由 <see cref="ParquetDataSyncService.SyncSettlementsAsync"/>
/// 方法同步 Oracle 数据后注册。
/// </para>
/// </remarks>
public sealed class DuckDbSettlementDataRepository : ISettlementDataRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<DuckDbSettlementDataRepository> _logger;

    /// <summary>
    /// 初始化 DuckDB 结算数据仓储
    /// </summary>
    /// <param name="connectionFactory">DuckDB 连接工厂（通过 Keyed Services "duckdb" 注入）</param>
    /// <param name="logger">日志记录器</param>
    public DuckDbSettlementDataRepository(
        [FromKeyedServices("duckdb")] IDbConnectionFactory connectionFactory,
        ILogger<DuckDbSettlementDataRepository>? logger = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? NullLogger<DuckDbSettlementDataRepository>.Instance;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Settlement>> QueryAllSettlementsAsync(
        SettlementQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        //_logger.LogDebug("从 DuckDB 查询全部结算数据，医院编码：{InstitutionCode}", filter.InstitutionCode);

        var connection = _connectionFactory.CreateConnection();

        // 诊断：查询视图的列名
        var columnCommand = (DbCommand)connection.CreateCommand();
        columnCommand.CommandText = "SELECT column_name FROM information_schema.columns WHERE table_name = '_settlements' ORDER BY ordinal_position";
        var columnReader = await columnCommand.ExecuteReaderAsync(cancellationToken);
        var columnNames = new List<string>();
        while (await columnReader.ReadAsync(cancellationToken))
        {
            columnNames.Add(columnReader.GetString(0));
        }
        _logger.LogDebug("诊断 - _settlements 视图列名：{ColumnNames}", string.Join(", ", columnNames));

        #region 测试
        // 诊断：查询前 5 条数据的 InstitutionCode 值
        //var diagCommand = (DbCommand)connection.CreateCommand();
        //diagCommand.CommandText = "SELECT DISTINCT InstitutionCode FROM _settlements LIMIT 5";
        //var diagReader = await diagCommand.ExecuteReaderAsync(cancellationToken);
        // var sampleCodes = new List<string>();
        // while (await diagReader.ReadAsync(cancellationToken))
        // {
        //     sampleCodes.Add(diagReader.IsDBNull(0) ? "NULL" : diagReader.GetString(0));
        // }
        // _logger.LogDebug("诊断 - Parquet 文件中 InstitutionCode 样本值：{SampleCodes}", string.Join(", ", sampleCodes));
        #endregion

        // 诊断：使用字符串插值构建 SQL，验证是否是参数绑定的问题
        var conditions = new List<string>();
        if (!string.IsNullOrWhiteSpace(filter.InstitutionCode))
        {
            conditions.Add($"InstitutionCode = '{filter.InstitutionCode}'");
        }
        if (!string.IsNullOrWhiteSpace(filter.SettlementId))
        {
            conditions.Add($"SettlementId = '{filter.SettlementId}'");
        }
        var whereClause = conditions.Count > 0 ? $" WHERE {string.Join(" AND ", conditions)}" : string.Empty;
        var sql = $"SELECT * FROM _settlements{whereClause}";

        _logger.LogDebug("执行 SQL (字符串插值): {Sql}", sql);

        var dbCommand = (DbCommand)connection.CreateCommand();
        dbCommand.CommandText = sql;

        var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);
        var results = new List<Settlement>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapToSettlement(reader));
        }

        _logger.LogDebug("从 DuckDB 查询到 {Count} 条结算数据", results.Count);
        #region 输出结果集
        var options = new JsonSerializerOptions
        {
            WriteIndented = false,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.All)
        };
        _logger.LogDebug("查询DuckDB结算数据结果集:{Results}", JsonSerializer.Serialize(results, options));
        #endregion
        return results;
    }

    /// <inheritdoc />
    public async Task<int> CountSettlementsAsync(
        SettlementQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        _logger.LogDebug("从 DuckDB 统计结算数据总数");

        var connection = _connectionFactory.CreateConnection();

        var (whereClause, parameters) = BuildWhereClause(filter);
        var sql = $"SELECT COUNT(*) FROM _settlements {whereClause}";

        _logger.LogDebug("执行 SQL: {Sql}", sql);

        var dbCommand = (DbCommand)connection.CreateCommand();
        dbCommand.CommandText = sql;

        foreach (var param in parameters)
        {
            var duckParam = new DuckDBParameter(param.Key) { Value = param.Value ?? DBNull.Value };
            dbCommand.Parameters.Add(duckParam);
        }

        var result = await dbCommand.ExecuteScalarAsync(cancellationToken);
        var count = Convert.ToInt32(result);

        _logger.LogDebug("DuckDB 结算数据总数: {Count}", count);
        return count;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Settlement>> QuerySettlementsAsync(
        SettlementQueryFilter filter,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(filter);

        _logger.LogDebug("从 DuckDB 分页查询结算数据，页码: {Page}，每页: {PageSize}", filter.Page, filter.PageSize);

        var connection = _connectionFactory.CreateConnection();

        var (whereClause, parameters) = BuildWhereClause(filter);
        var offset = (filter.Page - 1) * filter.PageSize;
        var sql = $"SELECT * FROM _settlements{whereClause} LIMIT {filter.PageSize} OFFSET {offset}";

        _logger.LogDebug("执行 SQL: {Sql}", sql);

        var dbCommand = (DbCommand)connection.CreateCommand();
        dbCommand.CommandText = sql;

        foreach (var param in parameters)
        {
            var duckParam = new DuckDBParameter(param.Key) { Value = param.Value ?? DBNull.Value };
            dbCommand.Parameters.Add(duckParam);
        }

        var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);
        var results = new List<Settlement>();

        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(MapToSettlement(reader));
        }

        _logger.LogDebug("从 DuckDB 分页查询到 {Count} 条结算数据", results.Count);
        return results;
    }

    /// <summary>
    /// 构建 WHERE 子句和参数列表
    /// </summary>
    /// <remarks>
    /// <para>
    /// DuckDB 位置参数 $N 需要参数名为数字字符串（如 "1", "2"），与 $N 中的 N 对应。
    /// </para>
    /// </remarks>
    private static (string WhereClause, List<KeyValuePair<string, object?>> Parameters) BuildWhereClause(
        SettlementQueryFilter filter)
    {
        var conditions = new List<string>();
        var parameters = new List<KeyValuePair<string, object?>>();
        var paramIndex = 1;

        if (!string.IsNullOrWhiteSpace(filter.InstitutionCode))
        {
            conditions.Add($"InstitutionCode = ${paramIndex}");
            parameters.Add(new(paramIndex.ToString(), filter.InstitutionCode));
            paramIndex++;
        }

        if (!string.IsNullOrWhiteSpace(filter.PersonnelNo))
        {
            conditions.Add($"PersonnelNo = ${paramIndex}");
            parameters.Add(new(paramIndex.ToString(), filter.PersonnelNo));
            paramIndex++;
        }

        if (!string.IsNullOrWhiteSpace(filter.MedicalRecordNo))
        {
            conditions.Add($"MedicalRecordNo = ${paramIndex}");
            parameters.Add(new(paramIndex.ToString(), filter.MedicalRecordNo));
            paramIndex++;
        }

        if (!string.IsNullOrWhiteSpace(filter.InsuranceType))
        {
            conditions.Add($"InsuType = ${paramIndex}");
            parameters.Add(new(paramIndex.ToString(), filter.InsuranceType));
            paramIndex++;
        }

        if (!string.IsNullOrWhiteSpace(filter.MedicalCategory))
        {
            conditions.Add($"MedicalCategory = ${paramIndex}");
            parameters.Add(new(paramIndex.ToString(), filter.MedicalCategory));
            paramIndex++;
        }

        var whereClause = conditions.Count > 0 ? $" WHERE {string.Join(" AND ", conditions)}" : string.Empty;
        return (whereClause, parameters);
    }

    /// <summary>
    /// 将 DataReader 映射为 Settlement 对象
    /// </summary>
    private static Settlement MapToSettlement(DbDataReader reader)
    {
        return new Settlement
        {
            VisitId = reader.IsDBNull(reader.GetOrdinal("VisitId")) ? null : reader.GetString(reader.GetOrdinal("VisitId")),
            SettlementId = reader.IsDBNull(reader.GetOrdinal("SettlementId")) ? null : reader.GetString(reader.GetOrdinal("SettlementId")),
            AccountingSerialNo = reader.IsDBNull(reader.GetOrdinal("AccountingSerialNo")) ? null : reader.GetString(reader.GetOrdinal("AccountingSerialNo")),
            ValidFlag = reader.IsDBNull(reader.GetOrdinal("ValidFlag")) ? null : reader.GetString(reader.GetOrdinal("ValidFlag")),
            PrescriptionOrderNo = reader.IsDBNull(reader.GetOrdinal("PrescriptionOrderNo")) ? null : reader.GetString(reader.GetOrdinal("PrescriptionOrderNo")),
            InstitutionCode = reader.IsDBNull(reader.GetOrdinal("InstitutionCode")) ? null : reader.GetString(reader.GetOrdinal("InstitutionCode")),
            InstitutionName = reader.IsDBNull(reader.GetOrdinal("InstitutionName")) ? null : reader.GetString(reader.GetOrdinal("InstitutionName")),
            PersonnelNo = reader.IsDBNull(reader.GetOrdinal("PersonnelNo")) ? null : reader.GetString(reader.GetOrdinal("PersonnelNo")),
            PersonnelName = reader.IsDBNull(reader.GetOrdinal("PersonnelName")) ? null : reader.GetString(reader.GetOrdinal("PersonnelName")),
            IDType = reader.IsDBNull(reader.GetOrdinal("IDType")) ? null : reader.GetString(reader.GetOrdinal("IDType")),
            IDNumber = reader.IsDBNull(reader.GetOrdinal("IDNumber")) ? null : reader.GetString(reader.GetOrdinal("IDNumber")),
            Age = reader.IsDBNull(reader.GetOrdinal("Age")) ? null : reader.GetDecimal(reader.GetOrdinal("Age")),
            MedicalRecordNo = reader.IsDBNull(reader.GetOrdinal("MedicalRecordNo")) ? null : reader.GetString(reader.GetOrdinal("MedicalRecordNo")),
            InpatientOutpatientNo = reader.IsDBNull(reader.GetOrdinal("InpatientOutpatientNo")) ? null : reader.GetString(reader.GetOrdinal("InpatientOutpatientNo")),
            HospitalDays = reader.IsDBNull(reader.GetOrdinal("HospitalDays")) ? null : reader.GetDecimal(reader.GetOrdinal("HospitalDays")),
            StartDate = reader.IsDBNull(reader.GetOrdinal("StartDate")) ? null : reader.GetString(reader.GetOrdinal("StartDate")),
            EndDate = reader.IsDBNull(reader.GetOrdinal("EndDate")) ? null : reader.GetString(reader.GetOrdinal("EndDate")),
            SettlementTime = reader.IsDBNull(reader.GetOrdinal("SettlementTime")) ? null : reader.GetString(reader.GetOrdinal("SettlementTime")),
            PrimaryDiagnosisName = reader.IsDBNull(reader.GetOrdinal("PrimaryDiagnosisName")) ? null : reader.GetString(reader.GetOrdinal("PrimaryDiagnosisName")),
            AdmissionDeptName = reader.IsDBNull(reader.GetOrdinal("AdmissionDeptName")) ? null : reader.GetString(reader.GetOrdinal("AdmissionDeptName")),
            DischargeDeptName = reader.IsDBNull(reader.GetOrdinal("DischargeDeptName")) ? null : reader.GetString(reader.GetOrdinal("DischargeDeptName")),
            InsuranceRelationId = reader.IsDBNull(reader.GetOrdinal("InsuranceRelationId")) ? null : reader.GetString(reader.GetOrdinal("InsuranceRelationId")),
            InsuranceRegion = reader.IsDBNull(reader.GetOrdinal("InsuranceRegion")) ? null : reader.GetString(reader.GetOrdinal("InsuranceRegion")),
            InsuranceType1 = reader.IsDBNull(reader.GetOrdinal("InsuranceType1")) ? null : reader.GetString(reader.GetOrdinal("InsuranceType1")),
            InsuType = reader.IsDBNull(reader.GetOrdinal("InsuType")) ? null : reader.GetString(reader.GetOrdinal("InsuType")),
            PaymentLocationType1 = reader.IsDBNull(reader.GetOrdinal("PaymentLocationType1")) ? null : reader.GetString(reader.GetOrdinal("PaymentLocationType1")),
            PaymentLocationType = reader.IsDBNull(reader.GetOrdinal("PaymentLocationType")) ? null : reader.GetString(reader.GetOrdinal("PaymentLocationType")),
            MedicalCategory1 = reader.IsDBNull(reader.GetOrdinal("MedicalCategory1")) ? null : reader.GetString(reader.GetOrdinal("MedicalCategory1")),
            MedicalCategory = reader.IsDBNull(reader.GetOrdinal("MedicalCategory")) ? null : reader.GetString(reader.GetOrdinal("MedicalCategory")),
            EntryMode = reader.IsDBNull(reader.GetOrdinal("EntryMode")) ? null : reader.GetString(reader.GetOrdinal("EntryMode")),
            DataSplit = reader.IsDBNull(reader.GetOrdinal("DataSplit")) ? null : reader.GetString(reader.GetOrdinal("DataSplit")),
            FeeDetailSerialNo = reader.IsDBNull(reader.GetOrdinal("FeeDetailSerialNo")) ? null : reader.GetString(reader.GetOrdinal("FeeDetailSerialNo")),
            FeeOccurrenceTime = reader.IsDBNull(reader.GetOrdinal("FeeOccurrenceTime")) ? null : reader.GetString(reader.GetOrdinal("FeeOccurrenceTime")),
            Quantity = reader.IsDBNull(reader.GetOrdinal("Quantity")) ? null : reader.GetDecimal(reader.GetOrdinal("Quantity")),
            UnitPrice = reader.IsDBNull(reader.GetOrdinal("UnitPrice")) ? null : reader.GetDecimal(reader.GetOrdinal("UnitPrice")),
            FeeDetailTotalAmount = reader.IsDBNull(reader.GetOrdinal("FeeDetailTotalAmount")) ? null : reader.GetDecimal(reader.GetOrdinal("FeeDetailTotalAmount")),
            PricingCapAmount = reader.IsDBNull(reader.GetOrdinal("PricingCapAmount")) ? null : reader.GetDecimal(reader.GetOrdinal("PricingCapAmount")),
            SelfPayRatio = reader.IsDBNull(reader.GetOrdinal("SelfPayRatio")) ? null : reader.GetDecimal(reader.GetOrdinal("SelfPayRatio")),
            PrePaymentType = reader.IsDBNull(reader.GetOrdinal("PrePaymentType")) ? null : reader.GetString(reader.GetOrdinal("PrePaymentType")),
            FullSelfPayAmount = reader.IsDBNull(reader.GetOrdinal("FullSelfPayAmount")) ? null : reader.GetDecimal(reader.GetOrdinal("FullSelfPayAmount")),
            OverLimitAmount = reader.IsDBNull(reader.GetOrdinal("OverLimitAmount")) ? null : reader.GetDecimal(reader.GetOrdinal("OverLimitAmount")),
            AdvanceSelfPayAmount = reader.IsDBNull(reader.GetOrdinal("AdvanceSelfPayAmount")) ? null : reader.GetDecimal(reader.GetOrdinal("AdvanceSelfPayAmount")),
            InScopeAmount = reader.IsDBNull(reader.GetOrdinal("InScopeAmount")) ? null : reader.GetDecimal(reader.GetOrdinal("InScopeAmount")),
            CivilServantBedAmount = reader.IsDBNull(reader.GetOrdinal("CivilServantBedAmount")) ? null : reader.GetDecimal(reader.GetOrdinal("CivilServantBedAmount")),
            HospitalDiscountAmount = reader.IsDBNull(reader.GetOrdinal("HospitalDiscountAmount")) ? null : reader.GetDecimal(reader.GetOrdinal("HospitalDiscountAmount")),
            HospitalAdvanceAmount = reader.IsDBNull(reader.GetOrdinal("HospitalAdvanceAmount")) ? null : reader.GetDecimal(reader.GetOrdinal("HospitalAdvanceAmount")),
            ChargeItemLevel = reader.IsDBNull(reader.GetOrdinal("ChargeItemLevel")) ? null : reader.GetString(reader.GetOrdinal("ChargeItemLevel")),
            InsuranceCatalogCode = reader.IsDBNull(reader.GetOrdinal("InsuranceCatalogCode")) ? null : reader.GetString(reader.GetOrdinal("InsuranceCatalogCode")),
            InsuranceCatalogName = reader.IsDBNull(reader.GetOrdinal("InsuranceCatalogName")) ? null : reader.GetString(reader.GetOrdinal("InsuranceCatalogName")),
            CatalogCategory = reader.IsDBNull(reader.GetOrdinal("CatalogCategory")) ? null : reader.GetString(reader.GetOrdinal("CatalogCategory")),
            MedicalCatalogCode = reader.IsDBNull(reader.GetOrdinal("MedicalCatalogCode")) ? null : reader.GetString(reader.GetOrdinal("MedicalCatalogCode")),
            InstitutionCatalogCode = reader.IsDBNull(reader.GetOrdinal("InstitutionCatalogCode")) ? null : reader.GetString(reader.GetOrdinal("InstitutionCatalogCode")),
            InstitutionCatalogName = reader.IsDBNull(reader.GetOrdinal("InstitutionCatalogName")) ? null : reader.GetString(reader.GetOrdinal("InstitutionCatalogName")),
            MedicalChargeCategory1 = reader.IsDBNull(reader.GetOrdinal("MedicalChargeCategory1")) ? null : reader.GetString(reader.GetOrdinal("MedicalChargeCategory1")),
            MedicalChargeCategory = reader.IsDBNull(reader.GetOrdinal("MedicalChargeCategory")) ? null : reader.GetString(reader.GetOrdinal("MedicalChargeCategory")),
            Specification = reader.IsDBNull(reader.GetOrdinal("Specification")) ? null : reader.GetString(reader.GetOrdinal("Specification")),
            DosageFormName = reader.IsDBNull(reader.GetOrdinal("DosageFormName")) ? null : reader.GetString(reader.GetOrdinal("DosageFormName")),
            OrderingDeptCode = reader.IsDBNull(reader.GetOrdinal("OrderingDeptCode")) ? null : reader.GetString(reader.GetOrdinal("OrderingDeptCode")),
            OrderingDeptName = reader.IsDBNull(reader.GetOrdinal("OrderingDeptName")) ? null : reader.GetString(reader.GetOrdinal("OrderingDeptName")),
            OrderingDoctorCode = reader.IsDBNull(reader.GetOrdinal("OrderingDoctorCode")) ? null : reader.GetString(reader.GetOrdinal("OrderingDoctorCode")),
            OrderingDoctorName = reader.IsDBNull(reader.GetOrdinal("OrderingDoctorName")) ? null : reader.GetString(reader.GetOrdinal("OrderingDoctorName")),
            ReceivingDeptCode = reader.IsDBNull(reader.GetOrdinal("ReceivingDeptCode")) ? null : reader.GetString(reader.GetOrdinal("ReceivingDeptCode")),
            ReceivingDeptName = reader.IsDBNull(reader.GetOrdinal("ReceivingDeptName")) ? null : reader.GetString(reader.GetOrdinal("ReceivingDeptName")),
            ReceivingDoctorCode = reader.IsDBNull(reader.GetOrdinal("ReceivingDoctorCode")) ? null : reader.GetString(reader.GetOrdinal("ReceivingDoctorCode")),
            ReceivingDoctorName = reader.IsDBNull(reader.GetOrdinal("ReceivingDoctorName")) ? null : reader.GetString(reader.GetOrdinal("ReceivingDoctorName"))
        };
    }
}
