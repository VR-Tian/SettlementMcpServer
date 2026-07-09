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
        // 根据 Oracle 表实际列名更新映射
        ["就诊ID"] = nameof(Settlement.VisitId),
        ["结算ID"] = nameof(Settlement.SettlementId),
        ["医院编码"] = nameof(Settlement.InstitutionCode),
        ["医院名称"] = nameof(Settlement.InstitutionName),
        ["人员编码"] = nameof(Settlement.PersonnelNo),
        ["年龄"] = nameof(Settlement.Age),
        ["病历号"] = nameof(Settlement.MedicalRecordNo),
        ["住院门诊号"] = nameof(Settlement.InpatientOutpatientNo),
        ["医疗类别"] = nameof(Settlement.MedicalCategory),
        ["开始时间"] = nameof(Settlement.StartDate),
        ["结束时间"] = nameof(Settlement.EndDate),
        ["结算时间"] = nameof(Settlement.SettlementTime),
        ["住院天数"] = nameof(Settlement.HospitalDays),
        ["主诊断名称"] = nameof(Settlement.PrimaryDiagnosisName),
        ["入院科室名称"] = nameof(Settlement.AdmissionDeptName),
        ["出院科室名称"] = nameof(Settlement.DischargeDeptName),
        ["险种"] = nameof(Settlement.InsuranceType1),
        ["数据分割"] = nameof(Settlement.DataSplit),
        ["医保目录编码"] = nameof(Settlement.InsuranceCatalogCode),
        ["医保目录名称"] = nameof(Settlement.InsuranceCatalogName),
        ["医疗目录编码"] = nameof(Settlement.MedicalCatalogCode),
        ["医药机构目录编码"] = nameof(Settlement.InstitutionCatalogCode),
        ["医药机构目录名称"] = nameof(Settlement.InstitutionCatalogName),
        ["医疗收费项目类别"] = nameof(Settlement.MedicalChargeCategory),
        ["收费项目等级"] = nameof(Settlement.ChargeItemLevel),
        ["目录类别"] = nameof(Settlement.CatalogCategory),
        ["数量"] = nameof(Settlement.Quantity),
        ["单价"] = nameof(Settlement.UnitPrice),
        ["费用日期"] = nameof(Settlement.FeeOccurrenceTime),
        ["明细项目费用总额"] = nameof(Settlement.FeeDetailTotalAmount),
        ["符合范围金额"] = nameof(Settlement.InScopeAmount),
        ["开单科室名称"] = nameof(Settlement.OrderingDeptName),
        ["开单医生"] = nameof(Settlement.OrderingDoctorName),
        ["受单科室"] = nameof(Settlement.ReceivingDeptName),
        ["受单医生"] = nameof(Settlement.ReceivingDoctorName),
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
