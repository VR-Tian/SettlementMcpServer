using System.Text.Json.Serialization;
using SettlementMcpServer.Contracts;

namespace SettlementMcpServer.Models;

/// <summary>
/// 医保结算数据查询条件
/// </summary>
public class SettlementQueryFilter : IPagedQuery
{
    public string? VisitId { get; set; }
    public string? SettlementId { get; set; }
    public string? PersonnelNo { get; set; }
    public string? MedicalRecordNo { get; set; }
    public string? InpatientOutpatientNo { get; set; }
    public string? InsuranceType { get; set; }
    public string? MedicalCategory { get; set; }

    /// <summary>
    /// 定点医药机构编码（可选，精确匹配）
    /// </summary>
    public string? InstitutionCode { get; set; }

    /// <summary>
    /// 页码（从 1 开始，默认 1）
    /// </summary>
    public int Page { get; set; } = 1;

    /// <summary>
    /// 每页条数（默认 100）
    /// </summary>
    public int PageSize { get; set; } = 100;
}

/// <summary>
/// 医保结算数据结果 - 对应表 YB_医保结算全量数据
/// </summary>
public class Settlement
{
    [JsonPropertyName("就诊ID")]
    public string? VisitId { get; set; }

    [JsonPropertyName("结算ID")]
    public string? SettlementId { get; set; }

    [JsonPropertyName("记账流水号")]
    public string? AccountingSerialNo { get; set; }

    [JsonPropertyName("有效标志")]
    public string? ValidFlag { get; set; }

    [JsonPropertyName("处方医嘱号")]
    public string? PrescriptionOrderNo { get; set; }

    [JsonPropertyName("定点医药机构编号")]
    public string? InstitutionCode { get; set; }

    [JsonPropertyName("定点医药机构名称")]
    public string? InstitutionName { get; set; }

    [JsonPropertyName("人员编号")]
    public string? PersonnelNo { get; set; }

    [JsonPropertyName("人员姓名")]
    public string? PersonnelName { get; set; }

    [JsonPropertyName("人员证件类型")]
    public string? IDType { get; set; }

    [JsonPropertyName("证件号码")]
    public string? IDNumber { get; set; }

    [JsonPropertyName("年龄")]
    public decimal? Age { get; set; }

    [JsonPropertyName("病历号")]
    public string? MedicalRecordNo { get; set; }

    [JsonPropertyName("住院_门诊号")]
    public string? InpatientOutpatientNo { get; set; }

    [JsonPropertyName("住院天数")]
    public decimal? HospitalDays { get; set; }

    [JsonPropertyName("开始日期")]
    public string? StartDate { get; set; }

    [JsonPropertyName("结束日期")]
    public string? EndDate { get; set; }

    [JsonPropertyName("结算时间")]
    public string? SettlementTime { get; set; }

    [JsonPropertyName("住院主诊断名称")]
    public string? PrimaryDiagnosisName { get; set; }

    [JsonPropertyName("入院科室名称")]
    public string? AdmissionDeptName { get; set; }

    [JsonPropertyName("出院科室名称")]
    public string? DischargeDeptName { get; set; }

    [JsonPropertyName("人员参保关系ID")]
    public string? InsuranceRelationId { get; set; }

    [JsonPropertyName("参保所属医保区划")]
    public string? InsuranceRegion { get; set; }

    [JsonPropertyName("险种类型1")]
    public string? InsuranceType1 { get; set; }

    [JsonPropertyName("INSUTYPE")]
    public string? InsuType { get; set; }

    [JsonPropertyName("支付地点类别1")]
    public string? PaymentLocationType1 { get; set; }

    [JsonPropertyName("支付地点类别")]
    public string? PaymentLocationType { get; set; }

    [JsonPropertyName("医疗类别1")]
    public string? MedicalCategory1 { get; set; }

    [JsonPropertyName("医疗类别")]
    public string? MedicalCategory { get; set; }

    [JsonPropertyName("录入方式")]
    public string? EntryMode { get; set; }

    [JsonPropertyName("数据分割")]
    public string? DataSplit { get; set; }

    [JsonPropertyName("费用明细流水号")]
    public string? FeeDetailSerialNo { get; set; }

    [JsonPropertyName("费用发生时间")]
    public string? FeeOccurrenceTime { get; set; }

    [JsonPropertyName("数量")]
    public decimal? Quantity { get; set; }

    [JsonPropertyName("单价")]
    public decimal? UnitPrice { get; set; }

    [JsonPropertyName("明细项目费用总额")]
    public decimal? FeeDetailTotalAmount { get; set; }

    [JsonPropertyName("定价上限金额")]
    public decimal? PricingCapAmount { get; set; }

    [JsonPropertyName("自付比例")]
    public decimal? SelfPayRatio { get; set; }

    [JsonPropertyName("先支付类型")]
    public string? PrePaymentType { get; set; }

    [JsonPropertyName("全自费金额")]
    public decimal? FullSelfPayAmount { get; set; }

    [JsonPropertyName("超限价金额")]
    public decimal? OverLimitAmount { get; set; }

    [JsonPropertyName("先行自付金额")]
    public decimal? AdvanceSelfPayAmount { get; set; }

    [JsonPropertyName("符合范围金额")]
    public decimal? InScopeAmount { get; set; }

    [JsonPropertyName("公务员床位费金额")]
    public decimal? CivilServantBedAmount { get; set; }

    [JsonPropertyName("医院减免金额")]
    public decimal? HospitalDiscountAmount { get; set; }

    [JsonPropertyName("医院垫付金额")]
    public decimal? HospitalAdvanceAmount { get; set; }

    [JsonPropertyName("收费项目等级")]
    public string? ChargeItemLevel { get; set; }

    [JsonPropertyName("医保目录编码")]
    public string? InsuranceCatalogCode { get; set; }

    [JsonPropertyName("医保目录名称")]
    public string? InsuranceCatalogName { get; set; }

    [JsonPropertyName("目录类别")]
    public string? CatalogCategory { get; set; }

    [JsonPropertyName("医疗目录编码")]
    public string? MedicalCatalogCode { get; set; }

    [JsonPropertyName("医药机构目录编码")]
    public string? InstitutionCatalogCode { get; set; }

    [JsonPropertyName("医药机构目录名称")]
    public string? InstitutionCatalogName { get; set; }

    [JsonPropertyName("医疗收费项目类别1")]
    public string? MedicalChargeCategory1 { get; set; }

    [JsonPropertyName("医疗收费项目类别")]
    public string? MedicalChargeCategory { get; set; }

    [JsonPropertyName("规格")]
    public string? Specification { get; set; }

    [JsonPropertyName("剂型名称")]
    public string? DosageFormName { get; set; }

    [JsonPropertyName("开单科室编码")]
    public string? OrderingDeptCode { get; set; }

    [JsonPropertyName("开单科室名称")]
    public string? OrderingDeptName { get; set; }

    [JsonPropertyName("开单医师代码")]
    public string? OrderingDoctorCode { get; set; }

    [JsonPropertyName("开单医师姓名")]
    public string? OrderingDoctorName { get; set; }

    [JsonPropertyName("受单科室编码")]
    public string? ReceivingDeptCode { get; set; }

    [JsonPropertyName("受单科室名称")]
    public string? ReceivingDeptName { get; set; }

    [JsonPropertyName("受单医师代码")]
    public string? ReceivingDoctorCode { get; set; }

    [JsonPropertyName("受单医师姓名")]
    public string? ReceivingDoctorName { get; set; }
}
