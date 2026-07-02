using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parquet;
using Parquet.Schema;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;
using System.Data;
using System.Data.Common;

namespace SettlementMcpServer.Infrastructure.DataSync;

/// <summary>
/// 基于 Parquet 文件的数据同步服务实现
/// </summary>
/// <remarks>
/// <para>
/// 使用 Parquet.Net SDK 将 Oracle 数据导出为 Parquet 文件，然后在 DuckDB 中注册为可查询视图。
/// </para>
/// <para>
/// Parquet 文件保存在 <c>%TEMP%\SettlementMcpServer\duckdb\data\</c> 目录。
/// </para>
/// </remarks>
public sealed class ParquetDataSyncService : IDataSyncService
{
    private readonly IYuehaiSettlementDataRepository _yuehaiRepository;
    private readonly IAuditDataRepository _auditRepository;
    private readonly IDbConnectionFactory _duckDbConnectionFactory;
    private readonly ILogger<ParquetDataSyncService> _logger;
    private readonly string _dataDirectory;

    /// <summary>
    /// 初始化数据同步服务
    /// </summary>
    /// <param name="yuehaiRepository">医保结算结算数据仓储</param>
    /// <param name="auditRepository">审核数据仓储</param>
    /// <param name="duckDbConnectionFactory">DuckDB 连接工厂</param>
    /// <param name="logger">日志记录器</param>
    public ParquetDataSyncService(
        IYuehaiSettlementDataRepository yuehaiRepository,
        IAuditDataRepository auditRepository,
        [FromKeyedServices("duckdb")] IDbConnectionFactory duckDbConnectionFactory,
        ILogger<ParquetDataSyncService>? logger = null)
    {
        _yuehaiRepository = yuehaiRepository ?? throw new ArgumentNullException(nameof(yuehaiRepository));
        _auditRepository = auditRepository ?? throw new ArgumentNullException(nameof(auditRepository));
        _duckDbConnectionFactory = duckDbConnectionFactory ?? throw new ArgumentNullException(nameof(duckDbConnectionFactory));
        _logger = logger ?? NullLogger<ParquetDataSyncService>.Instance;

        // Parquet 数据目录
        _dataDirectory = Path.Combine(Path.GetTempPath(), "SettlementMcpServer", "duckdb", "data");
        Directory.CreateDirectory(_dataDirectory);
    }

    /// <inheritdoc />
    public async Task<DataSyncResult> SyncYuehaiSettlementsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始同步医保结算结算数据到 DuckDB");

        // 从 Oracle 读取全部数据
        var filter = new YuehaiSettlementQueryFilter();
        var allData = await _yuehaiRepository.QueryAllSettlementsAsync(filter, cancellationToken);

        _logger.LogInformation("从 Oracle 读取到 {Count} 条医保结算结算数据", allData.Count);

        // 生成 Parquet 文件路径
        var fileName = $"yuehai_settlements_{DateTime.Now:yyyyMMddHHmmss}.parquet";
        var filePath = Path.Combine(_dataDirectory, fileName);

        // 写入 Parquet 文件
        await WriteYuehaiSettlementsToParquetAsync(allData, filePath, cancellationToken);

        _logger.LogInformation("医保结算结算数据已写入 Parquet 文件: {FilePath}", filePath);

        // 在 DuckDB 中注册视图
        await RegisterParquetViewInDuckDbAsync("yuehai_settlements", filePath, cancellationToken);

        _logger.LogInformation("医保结算结算数据同步完成");

        return new DataSyncResult
        {
            RecordCount = allData.Count,
            FilePath = filePath,
            DataTypeName = "YuehaiSettlement",
            SyncTime = DateTime.Now
        };
    }

    /// <inheritdoc />
    public async Task<DataSyncResult> SyncAuditedResultsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始同步审核数据到 DuckDB");

        // 从 Oracle 读取全部数据
        var filter = new AuditedResultQueryFilter();
        var allData = await _auditRepository.QueryAllAuditedResultsAsync(filter, cancellationToken);

        _logger.LogInformation("从 Oracle 读取到 {Count} 条审核数据", allData.Count);

        // 生成 Parquet 文件路径
        var fileName = $"audited_results_{DateTime.Now:yyyyMMddHHmmss}.parquet";
        var filePath = Path.Combine(_dataDirectory, fileName);

        // 写入 Parquet 文件
        await WriteAuditedResultsToParquetAsync(allData, filePath, cancellationToken);

        _logger.LogInformation("审核数据已写入 Parquet 文件: {FilePath}", filePath);

        // 在 DuckDB 中注册视图
        await RegisterParquetViewInDuckDbAsync("audited_results", filePath, cancellationToken);

        _logger.LogInformation("审核数据同步完成");

        return new DataSyncResult
        {
            RecordCount = allData.Count,
            FilePath = filePath,
            DataTypeName = "AuditedResult",
            SyncTime = DateTime.Now
        };
    }

    /// <summary>
    /// 将医保结算结算数据写入 Parquet 文件
    /// </summary>
    private async Task WriteYuehaiSettlementsToParquetAsync(
        IReadOnlyList<YuehaiSettlement> data,
        string filePath,
        CancellationToken cancellationToken)
    {
        if (data.Count == 0)
        {
            throw new InvalidOperationException("没有数据可写入 Parquet 文件");
        }

        // 定义 Parquet Schema
        var schema = new ParquetSchema(
            new DataField<string?>("VisitId"),
            new DataField<string?>("SettlementId"),
            new DataField<string?>("AccountingSerialNo"),
            new DataField<string?>("ValidFlag"),
            new DataField<string?>("PrescriptionOrderNo"),
            new DataField<string?>("InstitutionCode"),
            new DataField<string?>("InstitutionName"),
            new DataField<string?>("PersonnelNo"),
            new DataField<string?>("PersonnelName"),
            new DataField<string?>("IDType"),
            new DataField<string?>("IDNumber"),
            new DataField<decimal?>("Age"),
            new DataField<string?>("MedicalRecordNo"),
            new DataField<string?>("InpatientOutpatientNo"),
            new DataField<decimal?>("HospitalDays"),
            new DataField<string?>("StartDate"),
            new DataField<string?>("EndDate"),
            new DataField<string?>("SettlementTime"),
            new DataField<string?>("PrimaryDiagnosisName"),
            new DataField<string?>("AdmissionDeptName"),
            new DataField<string?>("DischargeDeptName"),
            new DataField<string?>("InsuranceRelationId"),
            new DataField<string?>("InsuranceRegion"),
            new DataField<string?>("InsuranceType1"),
            new DataField<string?>("InsuType"),
            new DataField<string?>("PaymentLocationType1"),
            new DataField<string?>("PaymentLocationType"),
            new DataField<string?>("MedicalCategory1"),
            new DataField<string?>("MedicalCategory"),
            new DataField<string?>("EntryMode"),
            new DataField<string?>("DataSplit"),
            new DataField<string?>("FeeDetailSerialNo"),
            new DataField<string?>("FeeOccurrenceTime"),
            new DataField<decimal?>("Quantity"),
            new DataField<decimal?>("UnitPrice"),
            new DataField<decimal?>("FeeDetailTotalAmount"),
            new DataField<decimal?>("PricingCapAmount"),
            new DataField<decimal?>("SelfPayRatio"),
            new DataField<string?>("PrePaymentType"),
            new DataField<decimal?>("FullSelfPayAmount"),
            new DataField<decimal?>("OverLimitAmount"),
            new DataField<decimal?>("AdvanceSelfPayAmount"),
            new DataField<decimal?>("InScopeAmount"),
            new DataField<decimal?>("CivilServantBedAmount"),
            new DataField<decimal?>("HospitalDiscountAmount"),
            new DataField<decimal?>("HospitalAdvanceAmount"),
            new DataField<string?>("ChargeItemLevel"),
            new DataField<string?>("InsuranceCatalogCode"),
            new DataField<string?>("InsuranceCatalogName"),
            new DataField<string?>("CatalogCategory"),
            new DataField<string?>("MedicalCatalogCode"),
            new DataField<string?>("InstitutionCatalogCode"),
            new DataField<string?>("InstitutionCatalogName"),
            new DataField<string?>("MedicalChargeCategory1"),
            new DataField<string?>("MedicalChargeCategory"),
            new DataField<string?>("Specification"),
            new DataField<string?>("DosageFormName"),
            new DataField<string?>("OrderingDeptCode"),
            new DataField<string?>("OrderingDeptName"),
            new DataField<string?>("OrderingDoctorCode"),
            new DataField<string?>("OrderingDoctorName"),
            new DataField<string?>("ReceivingDeptCode"),
            new DataField<string?>("ReceivingDeptName"),
            new DataField<string?>("ReceivingDoctorCode"),
            new DataField<string?>("ReceivingDoctorName")
        );

        using var fileStream = File.Create(filePath);
        using var writer = await ParquetWriter.CreateAsync(schema, fileStream);
        using var groupWriter = writer.CreateRowGroup();

        // 写入数据
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[0], data.Select(d => d.VisitId).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[1], data.Select(d => d.SettlementId).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[2], data.Select(d => d.AccountingSerialNo).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[3], data.Select(d => d.ValidFlag).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[4], data.Select(d => d.PrescriptionOrderNo).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[5], data.Select(d => d.InstitutionCode).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[6], data.Select(d => d.InstitutionName).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[7], data.Select(d => d.PersonnelNo).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[8], data.Select(d => d.PersonnelName).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[9], data.Select(d => d.IDType).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[10], data.Select(d => d.IDNumber).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[11], data.Select(d => d.Age).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[12], data.Select(d => d.MedicalRecordNo).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[13], data.Select(d => d.InpatientOutpatientNo).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[14], data.Select(d => d.HospitalDays).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[15], data.Select(d => d.StartDate).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[16], data.Select(d => d.EndDate).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[17], data.Select(d => d.SettlementTime).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[18], data.Select(d => d.PrimaryDiagnosisName).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[19], data.Select(d => d.AdmissionDeptName).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[20], data.Select(d => d.DischargeDeptName).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[21], data.Select(d => d.InsuranceRelationId).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[22], data.Select(d => d.InsuranceRegion).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[23], data.Select(d => d.InsuranceType1).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[24], data.Select(d => d.InsuType).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[25], data.Select(d => d.PaymentLocationType1).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[26], data.Select(d => d.PaymentLocationType).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[27], data.Select(d => d.MedicalCategory1).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[28], data.Select(d => d.MedicalCategory).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[29], data.Select(d => d.EntryMode).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[30], data.Select(d => d.DataSplit).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[31], data.Select(d => d.FeeDetailSerialNo).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[32], data.Select(d => d.FeeOccurrenceTime).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[33], data.Select(d => d.Quantity).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[34], data.Select(d => d.UnitPrice).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[35], data.Select(d => d.FeeDetailTotalAmount).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[36], data.Select(d => d.PricingCapAmount).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[37], data.Select(d => d.SelfPayRatio).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[38], data.Select(d => d.PrePaymentType).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[39], data.Select(d => d.FullSelfPayAmount).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[40], data.Select(d => d.OverLimitAmount).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[41], data.Select(d => d.AdvanceSelfPayAmount).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[42], data.Select(d => d.InScopeAmount).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[43], data.Select(d => d.CivilServantBedAmount).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[44], data.Select(d => d.HospitalDiscountAmount).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[45], data.Select(d => d.HospitalAdvanceAmount).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[46], data.Select(d => d.ChargeItemLevel).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[47], data.Select(d => d.InsuranceCatalogCode).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[48], data.Select(d => d.InsuranceCatalogName).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[49], data.Select(d => d.CatalogCategory).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[50], data.Select(d => d.MedicalCatalogCode).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[51], data.Select(d => d.InstitutionCatalogCode).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[52], data.Select(d => d.InstitutionCatalogName).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[53], data.Select(d => d.MedicalChargeCategory1).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[54], data.Select(d => d.MedicalChargeCategory).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[55], data.Select(d => d.Specification).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[56], data.Select(d => d.DosageFormName).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[57], data.Select(d => d.OrderingDeptCode).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[58], data.Select(d => d.OrderingDeptName).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[59], data.Select(d => d.OrderingDoctorCode).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[60], data.Select(d => d.OrderingDoctorName).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[61], data.Select(d => d.ReceivingDeptCode).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[62], data.Select(d => d.ReceivingDeptName).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[63], data.Select(d => d.ReceivingDoctorCode).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[64], data.Select(d => d.ReceivingDoctorName).ToArray()), cancellationToken);
    }

    /// <summary>
    /// 将审核数据写入 Parquet 文件
    /// </summary>
    private async Task WriteAuditedResultsToParquetAsync(
        IReadOnlyList<AuditedResult> data,
        string filePath,
        CancellationToken cancellationToken)
    {
        if (data.Count == 0)
        {
            throw new InvalidOperationException("没有数据可写入 Parquet 文件");
        }

        // 定义 Parquet Schema
        var schema = new ParquetSchema(
            new DataField<string?>("Id"),
            new DataField<string?>("NegativeListCategory"),
            new DataField<string?>("BehaviorDescription"),
            new DataField<string?>("RelatedItems"),
            new DataField<string?>("ViolationExplanation"),
            new DataField<string?>("PolicyBasis"),
            new DataField<string?>("MedicalRecordNo"),
            new DataField<string?>("HospitalCode"),
            new DataField<string?>("HospitalName"),
            new DataField<string?>("HospitalLevel"),
            new DataField<string?>("DischargeDepartment"),
            new DataField<string?>("BusinessNo"),
            new DataField<string?>("AdmissionDate"),
            new DataField<string?>("DischargeDate"),
            new DataField<string?>("SettlementDate"),
            new DataField<decimal?>("HospitalDays"),
            new DataField<string?>("DischargeDiagnosisCode"),
            new DataField<string?>("DischargeDiagnosis"),
            new DataField<string?>("OtherDiagnosis"),
            new DataField<string?>("OtherDiagnosisName"),
            new DataField<string?>("InsuredNo"),
            new DataField<string?>("InsuredName"),
            new DataField<string?>("BirthDate"),
            new DataField<decimal?>("Age"),
            new DataField<string?>("Gender"),
            new DataField<string?>("VisitType"),
            new DataField<string?>("BenefitType"),
            new DataField<string?>("InsuranceType"),
            new DataField<string?>("PersonnelCategory"),
            new DataField<decimal?>("CaseTotalAmount"),
            new DataField<decimal?>("InsuranceAmount"),
            new DataField<string?>("DetailId"),
            new DataField<string?>("ItemDate"),
            new DataField<string?>("ItemCode"),
            new DataField<string?>("ItemName"),
            new DataField<string?>("HospitalItemCode"),
            new DataField<string?>("HospitalItemName"),
            new DataField<decimal?>("UnitPrice"),
            new DataField<decimal?>("Quantity"),
            new DataField<decimal?>("Amount"),
            new DataField<decimal?>("InInsuranceAmount"),
            new DataField<string?>("Department"),
            new DataField<string?>("Doctor"),
            new DataField<string?>("RuleCode"),
            new DataField<string?>("RuleName"),
            new DataField<string?>("ReasonExplanation"),
            new DataField<decimal?>("InvolvedDetailAmount"),
            new DataField<string?>("DocumentCategory"),
            new DataField<string?>("ListCode")
        );

        using var fileStream = File.Create(filePath);
        using var writer = await ParquetWriter.CreateAsync(schema, fileStream);
        using var groupWriter = writer.CreateRowGroup();

        // 写入数据
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[0], data.Select(d => d.Id).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[1], data.Select(d => d.NegativeListCategory).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[2], data.Select(d => d.BehaviorDescription).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[3], data.Select(d => d.RelatedItems).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[4], data.Select(d => d.ViolationExplanation).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[5], data.Select(d => d.PolicyBasis).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[6], data.Select(d => d.MedicalRecordNo).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[7], data.Select(d => d.HospitalCode).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[8], data.Select(d => d.HospitalName).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[9], data.Select(d => d.HospitalLevel).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[10], data.Select(d => d.DischargeDepartment).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[11], data.Select(d => d.BusinessNo).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[12], data.Select(d => d.AdmissionDate).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[13], data.Select(d => d.DischargeDate).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[14], data.Select(d => d.SettlementDate).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[15], data.Select(d => d.HospitalDays).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[16], data.Select(d => d.DischargeDiagnosisCode).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[17], data.Select(d => d.DischargeDiagnosis).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[18], data.Select(d => d.OtherDiagnosis).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[19], data.Select(d => d.OtherDiagnosisName).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[20], data.Select(d => d.InsuredNo).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[21], data.Select(d => d.InsuredName).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[22], data.Select(d => d.BirthDate).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[23], data.Select(d => d.Age).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[24], data.Select(d => d.Gender).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[25], data.Select(d => d.VisitType).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[26], data.Select(d => d.BenefitType).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[27], data.Select(d => d.InsuranceType).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[28], data.Select(d => d.PersonnelCategory).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[29], data.Select(d => d.CaseTotalAmount).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[30], data.Select(d => d.InsuranceAmount).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[31], data.Select(d => d.DetailId).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[32], data.Select(d => d.ItemDate).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[33], data.Select(d => d.ItemCode).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[34], data.Select(d => d.ItemName).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[35], data.Select(d => d.HospitalItemCode).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[36], data.Select(d => d.HospitalItemName).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[37], data.Select(d => d.UnitPrice).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[38], data.Select(d => d.Quantity).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[39], data.Select(d => d.Amount).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[40], data.Select(d => d.InInsuranceAmount).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[41], data.Select(d => d.Department).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[42], data.Select(d => d.Doctor).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[43], data.Select(d => d.RuleCode).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[44], data.Select(d => d.RuleName).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[45], data.Select(d => d.ReasonExplanation).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[46], data.Select(d => d.InvolvedDetailAmount).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[47], data.Select(d => d.DocumentCategory).ToArray()), cancellationToken);
        await groupWriter.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[48], data.Select(d => d.ListCode).ToArray()), cancellationToken);
    }

    /// <summary>
    /// 在 DuckDB 中注册 Parquet 文件为可查询视图
    /// </summary>
    private async Task RegisterParquetViewInDuckDbAsync(
        string viewName,
        string parquetFilePath,
        CancellationToken cancellationToken)
    {
        using var connection = _duckDbConnectionFactory.CreateConnection();
        connection.Open();

        // 创建或替换视图
        var sql = $"CREATE OR REPLACE VIEW {viewName} AS SELECT * FROM read_parquet('{parquetFilePath.Replace("\\", "\\\\")}')";

        using var command = connection.CreateCommand();
        command.CommandText = sql;
        var dbCommand = (DbCommand)command;
        await dbCommand.ExecuteNonQueryAsync(cancellationToken);

        _logger.LogDebug("已在 DuckDB 中注册视图: {ViewName}", viewName);
    }
}
