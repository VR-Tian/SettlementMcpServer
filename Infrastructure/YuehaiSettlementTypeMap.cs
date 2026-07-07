using Dapper;
using SettlementMcpServer.Models;

namespace SettlementMcpServer.Infrastructure;

/// <summary>
/// 自定义 Dapper 类型映射器 - 将医保结算表中文列名映射到英文属性名
/// </summary>
internal sealed class SettlementTypeMap : DapperTypeMapBase
{
    private static readonly Dictionary<string, string> _columnMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["就诊ID"] = nameof(Settlement.VisitId),
        ["结算ID"] = nameof(Settlement.SettlementId),
        ["记账流水号"] = nameof(Settlement.AccountingSerialNo),
        ["有效标志"] = nameof(Settlement.ValidFlag),
        ["处方医嘱号"] = nameof(Settlement.PrescriptionOrderNo),
        ["定点医药机构编号"] = nameof(Settlement.InstitutionCode),
        ["定点医药机构名称"] = nameof(Settlement.InstitutionName),
        ["人员编号"] = nameof(Settlement.PersonnelNo),
        ["人员姓名"] = nameof(Settlement.PersonnelName),
        ["人员证件类型"] = nameof(Settlement.IDType),
        ["证件号码"] = nameof(Settlement.IDNumber),
        ["年龄"] = nameof(Settlement.Age),
        ["病历号"] = nameof(Settlement.MedicalRecordNo),
        ["住院_门诊号"] = nameof(Settlement.InpatientOutpatientNo),
        ["住院天数"] = nameof(Settlement.HospitalDays),
        ["开始日期"] = nameof(Settlement.StartDate),
        ["结束日期"] = nameof(Settlement.EndDate),
        ["结算时间"] = nameof(Settlement.SettlementTime),
        ["住院主诊断名称"] = nameof(Settlement.PrimaryDiagnosisName),
        ["入院科室名称"] = nameof(Settlement.AdmissionDeptName),
        ["出院科室名称"] = nameof(Settlement.DischargeDeptName),
        ["人员参保关系ID"] = nameof(Settlement.InsuranceRelationId),
        ["参保所属医保区划"] = nameof(Settlement.InsuranceRegion),
        ["险种类型1"] = nameof(Settlement.InsuranceType1),
        ["INSUTYPE"] = nameof(Settlement.InsuType),
        ["支付地点类别1"] = nameof(Settlement.PaymentLocationType1),
        ["支付地点类别"] = nameof(Settlement.PaymentLocationType),
        ["医疗类别1"] = nameof(Settlement.MedicalCategory1),
        ["医疗类别"] = nameof(Settlement.MedicalCategory),
        ["录入方式"] = nameof(Settlement.EntryMode),
        ["数据分割"] = nameof(Settlement.DataSplit),
        ["费用明细流水号"] = nameof(Settlement.FeeDetailSerialNo),
        ["费用发生时间"] = nameof(Settlement.FeeOccurrenceTime),
        ["数量"] = nameof(Settlement.Quantity),
        ["单价"] = nameof(Settlement.UnitPrice),
        ["明细项目费用总额"] = nameof(Settlement.FeeDetailTotalAmount),
        ["定价上限金额"] = nameof(Settlement.PricingCapAmount),
        ["自付比例"] = nameof(Settlement.SelfPayRatio),
        ["先支付类型"] = nameof(Settlement.PrePaymentType),
        ["全自费金额"] = nameof(Settlement.FullSelfPayAmount),
        ["超限价金额"] = nameof(Settlement.OverLimitAmount),
        ["先行自付金额"] = nameof(Settlement.AdvanceSelfPayAmount),
        ["符合范围金额"] = nameof(Settlement.InScopeAmount),
        ["公务员床位费金额"] = nameof(Settlement.CivilServantBedAmount),
        ["医院减免金额"] = nameof(Settlement.HospitalDiscountAmount),
        ["医院垫付金额"] = nameof(Settlement.HospitalAdvanceAmount),
        ["收费项目等级"] = nameof(Settlement.ChargeItemLevel),
        ["医保目录编码"] = nameof(Settlement.InsuranceCatalogCode),
        ["医保目录名称"] = nameof(Settlement.InsuranceCatalogName),
        ["目录类别"] = nameof(Settlement.CatalogCategory),
        ["医疗目录编码"] = nameof(Settlement.MedicalCatalogCode),
        ["医药机构目录编码"] = nameof(Settlement.InstitutionCatalogCode),
        ["医药机构目录名称"] = nameof(Settlement.InstitutionCatalogName),
        ["医疗收费项目类别1"] = nameof(Settlement.MedicalChargeCategory1),
        ["医疗收费项目类别"] = nameof(Settlement.MedicalChargeCategory),
        ["规格"] = nameof(Settlement.Specification),
        ["剂型名称"] = nameof(Settlement.DosageFormName),
        ["开单科室编码"] = nameof(Settlement.OrderingDeptCode),
        ["开单科室名称"] = nameof(Settlement.OrderingDeptName),
        ["开单医师代码"] = nameof(Settlement.OrderingDoctorCode),
        ["开单医师姓名"] = nameof(Settlement.OrderingDoctorName),
        ["受单科室编码"] = nameof(Settlement.ReceivingDeptCode),
        ["受单科室名称"] = nameof(Settlement.ReceivingDeptName),
        ["受单医师代码"] = nameof(Settlement.ReceivingDoctorCode),
        ["受单医师姓名"] = nameof(Settlement.ReceivingDoctorName),
    };

    /// <summary>
    /// 列名 → 属性名映射字典（由基类要求实现）
    /// </summary>
    protected override Dictionary<string, string> ColumnMappings => _columnMappings;

    public static void Register()
    {
        SqlMapper.SetTypeMap(typeof(Settlement), new SettlementTypeMap(typeof(Settlement)));
    }

    private SettlementTypeMap(Type type) : base(type)
    {
    }
}
