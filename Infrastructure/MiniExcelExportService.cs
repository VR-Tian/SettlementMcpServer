using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiniExcelLibs;
using MiniExcelLibs.Attributes;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;

namespace SettlementMcpServer.Infrastructure;

/// <summary>
/// 基于 MiniExcel 的 Excel 导出服务实现
/// </summary>
/// <remarks>
/// <para>
/// 使用 <b>MiniExcel</b> 作为 Excel 生成组件，相比 EPPlus/ClosedXML 有以下优势：
/// <list type="bullet">
///   <item><description><b>低内存占用</b>：采用流式写入（Streaming），不将整个 Excel 文件加载到内存中，适合大数据量导出。</description></item>
///   <item><description><b>无外部依赖</b>：不需要安装 Office 或 COM 组件，跨平台支持 Linux/Windows/macOS。</description></item>
///   <item><description><b>高性能</b>：写入 10 万行数据仅需数秒，内存占用保持在 MB 级别。</description></item>
///   <item><description><b>零配置</b>：开箱即用，无需许可证（MIT 开源协议）。</description></item>
/// </list>
/// </para>
/// </remarks>
public sealed class MiniExcelExportService : IExcelExportService
{
    /// <summary>
    /// 导出文件子目录名称
    /// </summary>
    private const string ExportSubFolder = "SettlementMcpServer";

    /// <summary>
    /// 默认工作表名称
    /// </summary>
    private const string DefaultSheetName = "审核数据";

    private readonly ILogger<MiniExcelExportService> _logger;

    /// <summary>
    /// 初始化 Excel 导出服务实例
    /// </summary>
    /// <param name="logger">日志记录器（由 DI 注入，可选）</param>
    public MiniExcelExportService(ILogger<MiniExcelExportService>? logger = null)
    {
        _logger = logger ?? NullLogger<MiniExcelExportService>.Instance;
    }

    /// <inheritdoc />
    public async Task<string> ExportAuditedResultsToExcelAsync(
        IEnumerable<AuditedResult> data,
        string? sheetName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        var sheet = sheetName ?? DefaultSheetName;
        _logger.LogInformation("开始导出审核数据到 Excel，工作表: {SheetName}", sheet);

        // 将 AuditedResult 集合转换为 Excel 行模型集合
        // MiniExcel 通过特性标注（ExcelColumnName）将属性名映射到中文列名
        var excelRows = data.Select(MapToExcelRow).ToList();

        // 获取系统临时文件夹路径
        // Windows: %TEMP% (通常为 C:\Users\{用户名}\AppData\Local\Temp)
        // Linux/macOS: /tmp
        var tempFolder = Path.GetTempPath();
        var exportFolder = Path.Combine(tempFolder, ExportSubFolder);

        // 确保导出目录存在
        // 如果目录不存在则创建，已存在则跳过
        Directory.CreateDirectory(exportFolder);

        // 生成唯一文件名：AuditExport_yyyyMMddHHmmss_随机字符串.xlsx
        // 时间戳 + 随机字符串确保并发导出时文件名不冲突
        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var randomSuffix = Path.GetRandomFileName().Replace(".", "");
        var fileName = $"AuditExport_{timestamp}_{randomSuffix}.xlsx";
        var filePath = Path.Combine(exportFolder, fileName);

        // 使用 MiniExcel 流式写入 Excel 到文件
        // MiniExcel.SaveAsAsync 直接写入文件，内部使用 Open XML 格式流式写入，
        // 避免将整个文件加载到内存中。
        await MiniExcel.SaveAsAsync(filePath, excelRows, sheetName: sheet, cancellationToken: cancellationToken);

        var fileInfo = new FileInfo(filePath);
        var fileSizeKB = fileInfo.Length / 1024.0;

        _logger.LogInformation(
            "Excel 导出完成，文件路径: {FilePath}，文件大小: {FileSizeKB:F1} KB，数据行数: {RowCount}",
            filePath, fileSizeKB, excelRows.Count());

        return filePath;
    }

    /// <summary>
    /// 将 AuditedResult 实体映射为 Excel 行模型
    /// </summary>
    /// <param name="result">审核数据实体</param>
    /// <returns>Excel 行模型（属性通过 ExcelColumnName 特性映射到中文列名）</returns>
    private static AuditedResultExcelRow MapToExcelRow(AuditedResult result)
    {
        return new AuditedResultExcelRow
        {
            Id = result.Id,
            NegativeListCategory = result.NegativeListCategory,
            BehaviorDescription = result.BehaviorDescription,
            RelatedItems = result.RelatedItems,
            ViolationExplanation = result.ViolationExplanation,
            PolicyBasis = result.PolicyBasis,
            MedicalRecordNo = result.MedicalRecordNo,
            HospitalCode = result.HospitalCode,
            HospitalName = result.HospitalName,
            HospitalLevel = result.HospitalLevel,
            DischargeDepartment = result.DischargeDepartment,
            BusinessNo = result.BusinessNo,
            AdmissionDate = result.AdmissionDate,
            DischargeDate = result.DischargeDate,
            SettlementDate = result.SettlementDate,
            HospitalDays = result.HospitalDays,
            DischargeDiagnosisCode = result.DischargeDiagnosisCode,
            DischargeDiagnosis = result.DischargeDiagnosis,
            OtherDiagnosis = result.OtherDiagnosis,
            OtherDiagnosisName = result.OtherDiagnosisName,
            InsuredNo = result.InsuredNo,
            InsuredName = result.InsuredName,
            BirthDate = result.BirthDate,
            Age = result.Age,
            Gender = result.Gender,
            VisitType = result.VisitType,
            BenefitType = result.BenefitType,
            InsuranceType = result.InsuranceType,
            PersonnelCategory = result.PersonnelCategory,
            CaseTotalAmount = result.CaseTotalAmount,
            InsuranceAmount = result.InsuranceAmount,
            DetailId = result.DetailId,
            ItemDate = result.ItemDate,
            ItemCode = result.ItemCode,
            ItemName = result.ItemName,
            HospitalItemCode = result.HospitalItemCode,
            HospitalItemName = result.HospitalItemName,
            UnitPrice = result.UnitPrice,
            Quantity = result.Quantity,
            Amount = result.Amount,
            InInsuranceAmount = result.InInsuranceAmount,
            Department = result.Department,
            Doctor = result.Doctor,
            RuleCode = result.RuleCode,
            RuleName = result.RuleName,
            ReasonExplanation = result.ReasonExplanation,
            InvolvedDetailAmount = result.InvolvedDetailAmount,
            DocumentCategory = result.DocumentCategory,
            ListCode = result.ListCode,
        };
    }

    /// <inheritdoc />
    public async Task<string> ExportSettlementsToExcelAsync(
        IEnumerable<Settlement> data,
        string? sheetName = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        var sheet = sheetName ?? "结算数据";
        _logger.LogInformation("开始导出结算数据到 Excel，工作表: {SheetName}", sheet);

        var excelRows = data.Select(MapToExcelRow).ToList();

        var tempFolder = Path.GetTempPath();
        var exportFolder = Path.Combine(tempFolder, ExportSubFolder);
        Directory.CreateDirectory(exportFolder);

        var timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
        var randomSuffix = Path.GetRandomFileName().Replace(".", "");
        var fileName = $"SettlementExport_{timestamp}_{randomSuffix}.xlsx";
        var filePath = Path.Combine(exportFolder, fileName);

        await MiniExcel.SaveAsAsync(filePath, excelRows, sheetName: sheet, cancellationToken: cancellationToken);

        var fileInfo = new FileInfo(filePath);
        var fileSizeKB = fileInfo.Length / 1024.0;

        _logger.LogInformation(
            "结算数据 Excel 导出完成，文件路径: {FilePath}，文件大小: {FileSizeKB:F1} KB，数据行数: {RowCount}",
            filePath, fileSizeKB, excelRows.Count);

        return filePath;
    }

    /// <summary>
    /// 将 Settlement 实体映射为 Excel 行模型
    /// </summary>
    private static SettlementExcelRow MapToExcelRow(Settlement settlement)
    {
        return new SettlementExcelRow
        {
            VisitId = settlement.VisitId,
            SettlementId = settlement.SettlementId,
            AccountingSerialNo = settlement.AccountingSerialNo,
            ValidFlag = settlement.ValidFlag,
            PrescriptionOrderNo = settlement.PrescriptionOrderNo,
            InstitutionCode = settlement.InstitutionCode,
            InstitutionName = settlement.InstitutionName,
            PersonnelNo = settlement.PersonnelNo,
            PersonnelName = settlement.PersonnelName,
            IDType = settlement.IDType,
            IDNumber = settlement.IDNumber,
            Age = settlement.Age,
            MedicalRecordNo = settlement.MedicalRecordNo,
            InpatientOutpatientNo = settlement.InpatientOutpatientNo,
            HospitalDays = settlement.HospitalDays,
            StartDate = settlement.StartDate,
            EndDate = settlement.EndDate,
            SettlementTime = settlement.SettlementTime,
            PrimaryDiagnosisName = settlement.PrimaryDiagnosisName,
            AdmissionDeptName = settlement.AdmissionDeptName,
            DischargeDeptName = settlement.DischargeDeptName,
            InsuranceRelationId = settlement.InsuranceRelationId,
            InsuranceRegion = settlement.InsuranceRegion,
            InsuranceType1 = settlement.InsuranceType1,
            InsuType = settlement.InsuType,
            PaymentLocationType1 = settlement.PaymentLocationType1,
            PaymentLocationType = settlement.PaymentLocationType,
            MedicalCategory1 = settlement.MedicalCategory1,
            MedicalCategory = settlement.MedicalCategory,
            EntryMode = settlement.EntryMode,
            DataSplit = settlement.DataSplit,
            FeeDetailSerialNo = settlement.FeeDetailSerialNo,
            FeeOccurrenceTime = settlement.FeeOccurrenceTime,
            Quantity = settlement.Quantity,
            UnitPrice = settlement.UnitPrice,
            FeeDetailTotalAmount = settlement.FeeDetailTotalAmount,
            PricingCapAmount = settlement.PricingCapAmount,
            SelfPayRatio = settlement.SelfPayRatio,
            PrePaymentType = settlement.PrePaymentType,
            FullSelfPayAmount = settlement.FullSelfPayAmount,
            OverLimitAmount = settlement.OverLimitAmount,
            AdvanceSelfPayAmount = settlement.AdvanceSelfPayAmount,
            InScopeAmount = settlement.InScopeAmount,
            CivilServantBedAmount = settlement.CivilServantBedAmount,
            HospitalDiscountAmount = settlement.HospitalDiscountAmount,
            HospitalAdvanceAmount = settlement.HospitalAdvanceAmount,
            ChargeItemLevel = settlement.ChargeItemLevel,
            InsuranceCatalogCode = settlement.InsuranceCatalogCode,
            InsuranceCatalogName = settlement.InsuranceCatalogName,
            CatalogCategory = settlement.CatalogCategory,
            MedicalCatalogCode = settlement.MedicalCatalogCode,
            InstitutionCatalogCode = settlement.InstitutionCatalogCode,
            InstitutionCatalogName = settlement.InstitutionCatalogName,
            MedicalChargeCategory1 = settlement.MedicalChargeCategory1,
            MedicalChargeCategory = settlement.MedicalChargeCategory,
            Specification = settlement.Specification,
            DosageFormName = settlement.DosageFormName,
            OrderingDeptCode = settlement.OrderingDeptCode,
            OrderingDeptName = settlement.OrderingDeptName,
            OrderingDoctorCode = settlement.OrderingDoctorCode,
            OrderingDoctorName = settlement.OrderingDoctorName,
            ReceivingDeptCode = settlement.ReceivingDeptCode,
            ReceivingDeptName = settlement.ReceivingDeptName,
            ReceivingDoctorCode = settlement.ReceivingDoctorCode,
            ReceivingDoctorName = settlement.ReceivingDoctorName,
        };
    }

    /// <summary>
    /// 结算数据 Excel 行模型
    /// </summary>
    private sealed class SettlementExcelRow
    {
        [ExcelColumnName("就诊ID")]
        public string? VisitId { get; set; }

        [ExcelColumnName("结算ID")]
        public string? SettlementId { get; set; }

        [ExcelColumnName("记账流水号")]
        public string? AccountingSerialNo { get; set; }

        [ExcelColumnName("有效标志")]
        public string? ValidFlag { get; set; }

        [ExcelColumnName("处方医嘱号")]
        public string? PrescriptionOrderNo { get; set; }

        [ExcelColumnName("定点医药机构编号")]
        public string? InstitutionCode { get; set; }

        [ExcelColumnName("定点医药机构名称")]
        public string? InstitutionName { get; set; }

        [ExcelColumnName("人员编号")]
        public string? PersonnelNo { get; set; }

        [ExcelColumnName("人员姓名")]
        public string? PersonnelName { get; set; }

        [ExcelColumnName("人员证件类型")]
        public string? IDType { get; set; }

        [ExcelColumnName("证件号码")]
        public string? IDNumber { get; set; }

        [ExcelColumnName("年龄")]
        public decimal? Age { get; set; }

        [ExcelColumnName("病历号")]
        public string? MedicalRecordNo { get; set; }

        [ExcelColumnName("住院_门诊号")]
        public string? InpatientOutpatientNo { get; set; }

        [ExcelColumnName("住院天数")]
        public decimal? HospitalDays { get; set; }

        [ExcelColumnName("开始日期")]
        public string? StartDate { get; set; }

        [ExcelColumnName("结束日期")]
        public string? EndDate { get; set; }

        [ExcelColumnName("结算时间")]
        public string? SettlementTime { get; set; }

        [ExcelColumnName("住院主诊断名称")]
        public string? PrimaryDiagnosisName { get; set; }

        [ExcelColumnName("入院科室名称")]
        public string? AdmissionDeptName { get; set; }

        [ExcelColumnName("出院科室名称")]
        public string? DischargeDeptName { get; set; }

        [ExcelColumnName("人员参保关系ID")]
        public string? InsuranceRelationId { get; set; }

        [ExcelColumnName("参保所属医保区划")]
        public string? InsuranceRegion { get; set; }

        [ExcelColumnName("险种类型1")]
        public string? InsuranceType1 { get; set; }

        [ExcelColumnName("INSUTYPE")]
        public string? InsuType { get; set; }

        [ExcelColumnName("支付地点类别1")]
        public string? PaymentLocationType1 { get; set; }

        [ExcelColumnName("支付地点类别")]
        public string? PaymentLocationType { get; set; }

        [ExcelColumnName("医疗类别1")]
        public string? MedicalCategory1 { get; set; }

        [ExcelColumnName("医疗类别")]
        public string? MedicalCategory { get; set; }

        [ExcelColumnName("录入方式")]
        public string? EntryMode { get; set; }

        [ExcelColumnName("数据分割")]
        public string? DataSplit { get; set; }

        [ExcelColumnName("费用明细流水号")]
        public string? FeeDetailSerialNo { get; set; }

        [ExcelColumnName("费用发生时间")]
        public string? FeeOccurrenceTime { get; set; }

        [ExcelColumnName("数量")]
        public decimal? Quantity { get; set; }

        [ExcelColumnName("单价")]
        public decimal? UnitPrice { get; set; }

        [ExcelColumnName("明细项目费用总额")]
        public decimal? FeeDetailTotalAmount { get; set; }

        [ExcelColumnName("定价上限金额")]
        public decimal? PricingCapAmount { get; set; }

        [ExcelColumnName("自付比例")]
        public decimal? SelfPayRatio { get; set; }

        [ExcelColumnName("先支付类型")]
        public string? PrePaymentType { get; set; }

        [ExcelColumnName("全自费金额")]
        public decimal? FullSelfPayAmount { get; set; }

        [ExcelColumnName("超限价金额")]
        public decimal? OverLimitAmount { get; set; }

        [ExcelColumnName("先行自付金额")]
        public decimal? AdvanceSelfPayAmount { get; set; }

        [ExcelColumnName("符合范围金额")]
        public decimal? InScopeAmount { get; set; }

        [ExcelColumnName("公务员床位费金额")]
        public decimal? CivilServantBedAmount { get; set; }

        [ExcelColumnName("医院减免金额")]
        public decimal? HospitalDiscountAmount { get; set; }

        [ExcelColumnName("医院垫付金额")]
        public decimal? HospitalAdvanceAmount { get; set; }

        [ExcelColumnName("收费项目等级")]
        public string? ChargeItemLevel { get; set; }

        [ExcelColumnName("医保目录编码")]
        public string? InsuranceCatalogCode { get; set; }

        [ExcelColumnName("医保目录名称")]
        public string? InsuranceCatalogName { get; set; }

        [ExcelColumnName("目录类别")]
        public string? CatalogCategory { get; set; }

        [ExcelColumnName("医疗目录编码")]
        public string? MedicalCatalogCode { get; set; }

        [ExcelColumnName("医药机构目录编码")]
        public string? InstitutionCatalogCode { get; set; }

        [ExcelColumnName("医药机构目录名称")]
        public string? InstitutionCatalogName { get; set; }

        [ExcelColumnName("医疗收费项目类别1")]
        public string? MedicalChargeCategory1 { get; set; }

        [ExcelColumnName("医疗收费项目类别")]
        public string? MedicalChargeCategory { get; set; }

        [ExcelColumnName("规格")]
        public string? Specification { get; set; }

        [ExcelColumnName("剂型名称")]
        public string? DosageFormName { get; set; }

        [ExcelColumnName("开单科室编码")]
        public string? OrderingDeptCode { get; set; }

        [ExcelColumnName("开单科室名称")]
        public string? OrderingDeptName { get; set; }

        [ExcelColumnName("开单医师代码")]
        public string? OrderingDoctorCode { get; set; }

        [ExcelColumnName("开单医师姓名")]
        public string? OrderingDoctorName { get; set; }

        [ExcelColumnName("受单科室编码")]
        public string? ReceivingDeptCode { get; set; }

        [ExcelColumnName("受单科室名称")]
        public string? ReceivingDeptName { get; set; }

        [ExcelColumnName("受单医师代码")]
        public string? ReceivingDoctorCode { get; set; }

        [ExcelColumnName("受单医师姓名")]
        public string? ReceivingDoctorName { get; set; }
    }

    /// <summary>
    /// Excel 行数据模型
    /// </summary>
    /// <remarks>
    /// <para>
    /// 此类的每个属性都通过 <see cref="ExcelColumnNameAttribute"/> 标注中文列名，
    /// MiniExcel 会根据此特性生成对应的 Excel 列头。
    /// </para>
    /// <para>
    /// <b>为什么不直接使用 AuditedResult 作为 Excel 数据源？</b>
    /// </para>
    /// <list type="bullet">
    ///   <item><description>AuditedResult 的属性名为英文，Excel 列头需要显示中文。</description></item>
    ///   <item><description>使用独立行模型类可以将"数据查询模型"与"Excel 导出模型"解耦，各自职责独立。</description></item>
    ///   <item><description>未来如需调整 Excel 列头顺序或添加计算列，只需修改此模型，不影响查询模型。</description></item>
    /// </list>
    /// </remarks>
    private sealed class AuditedResultExcelRow
    {
        [ExcelColumnName("ID")]
        public string? Id { get; set; }

        [ExcelColumnName("负面清单行为类别")]
        public string? NegativeListCategory { get; set; }

        [ExcelColumnName("行为释义")]
        public string? BehaviorDescription { get; set; }

        [ExcelColumnName("相关项目")]
        public string? RelatedItems { get; set; }

        [ExcelColumnName("违规说明")]
        public string? ViolationExplanation { get; set; }

        [ExcelColumnName("政策依据")]
        public string? PolicyBasis { get; set; }

        [ExcelColumnName("病案号")]
        public string? MedicalRecordNo { get; set; }

        [ExcelColumnName("医院编码")]
        public string? HospitalCode { get; set; }

        [ExcelColumnName("医院名称")]
        public string? HospitalName { get; set; }

        [ExcelColumnName("医院级别")]
        public string? HospitalLevel { get; set; }

        [ExcelColumnName("出院科室")]
        public string? DischargeDepartment { get; set; }

        [ExcelColumnName("业务号")]
        public string? BusinessNo { get; set; }

        [ExcelColumnName("入院日期")]
        public string? AdmissionDate { get; set; }

        [ExcelColumnName("出院日期")]
        public string? DischargeDate { get; set; }

        [ExcelColumnName("结算日期")]
        public string? SettlementDate { get; set; }

        [ExcelColumnName("住院日")]
        public decimal? HospitalDays { get; set; }

        [ExcelColumnName("出院诊断编码")]
        public string? DischargeDiagnosisCode { get; set; }

        [ExcelColumnName("出院诊断")]
        public string? DischargeDiagnosis { get; set; }

        [ExcelColumnName("其他诊断")]
        public string? OtherDiagnosis { get; set; }

        [ExcelColumnName("其他诊断名称")]
        public string? OtherDiagnosisName { get; set; }

        [ExcelColumnName("参保人号")]
        public string? InsuredNo { get; set; }

        [ExcelColumnName("参保人")]
        public string? InsuredName { get; set; }

        [ExcelColumnName("出生日期")]
        public string? BirthDate { get; set; }

        [ExcelColumnName("年龄")]
        public decimal? Age { get; set; }

        [ExcelColumnName("性别")]
        public string? Gender { get; set; }

        [ExcelColumnName("就医方式")]
        public string? VisitType { get; set; }

        [ExcelColumnName("待遇类型")]
        public string? BenefitType { get; set; }

        [ExcelColumnName("参保类型")]
        public string? InsuranceType { get; set; }

        [ExcelColumnName("人员类别")]
        public string? PersonnelCategory { get; set; }

        [ExcelColumnName("病例总金额")]
        public decimal? CaseTotalAmount { get; set; }

        [ExcelColumnName("医保金额")]
        public decimal? InsuranceAmount { get; set; }

        [ExcelColumnName("明细ID")]
        public string? DetailId { get; set; }

        [ExcelColumnName("项目日期")]
        public string? ItemDate { get; set; }

        [ExcelColumnName("项目编码")]
        public string? ItemCode { get; set; }

        [ExcelColumnName("项目名称")]
        public string? ItemName { get; set; }

        [ExcelColumnName("医院项目编码")]
        public string? HospitalItemCode { get; set; }

        [ExcelColumnName("医院项目名称")]
        public string? HospitalItemName { get; set; }

        [ExcelColumnName("单价")]
        public decimal? UnitPrice { get; set; }

        [ExcelColumnName("数量")]
        public decimal? Quantity { get; set; }

        [ExcelColumnName("金额")]
        public decimal? Amount { get; set; }

        [ExcelColumnName("医保内金额")]
        public decimal? InInsuranceAmount { get; set; }

        [ExcelColumnName("科室")]
        public string? Department { get; set; }

        [ExcelColumnName("医生")]
        public string? Doctor { get; set; }

        [ExcelColumnName("规则编码")]
        public string? RuleCode { get; set; }

        [ExcelColumnName("规则名称")]
        public string? RuleName { get; set; }

        [ExcelColumnName("理由说明")]
        public string? ReasonExplanation { get; set; }

        [ExcelColumnName("涉及明细金额")]
        public decimal? InvolvedDetailAmount { get; set; }

        [ExcelColumnName("单据类别")]
        public string? DocumentCategory { get; set; }

        [ExcelColumnName("清单编码")]
        public string? ListCode { get; set; }
    }
}
