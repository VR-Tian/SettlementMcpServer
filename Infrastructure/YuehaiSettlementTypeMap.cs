using Dapper;
using SettlementMcpServer.Models;
using System.Reflection;

namespace SettlementMcpServer.Infrastructure;

/// <summary>
/// 自定义 Dapper 类型映射器 - 将粤海医保结算表中文列名映射到英文属性名
/// </summary>
internal sealed class YuehaiSettlementTypeMap : SqlMapper.ITypeMap
{
    private static readonly Dictionary<string, string> ColumnMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["就诊ID"] = nameof(YuehaiSettlement.VisitId),
        ["结算ID"] = nameof(YuehaiSettlement.SettlementId),
        ["记账流水号"] = nameof(YuehaiSettlement.AccountingSerialNo),
        ["有效标志"] = nameof(YuehaiSettlement.ValidFlag),
        ["处方医嘱号"] = nameof(YuehaiSettlement.PrescriptionOrderNo),
        ["定点医药机构编号"] = nameof(YuehaiSettlement.InstitutionCode),
        ["定点医药机构名称"] = nameof(YuehaiSettlement.InstitutionName),
        ["人员编号"] = nameof(YuehaiSettlement.PersonnelNo),
        ["人员姓名"] = nameof(YuehaiSettlement.PersonnelName),
        ["人员证件类型"] = nameof(YuehaiSettlement.IDType),
        ["证件号码"] = nameof(YuehaiSettlement.IDNumber),
        ["年龄"] = nameof(YuehaiSettlement.Age),
        ["病历号"] = nameof(YuehaiSettlement.MedicalRecordNo),
        ["住院_门诊号"] = nameof(YuehaiSettlement.InpatientOutpatientNo),
        ["住院天数"] = nameof(YuehaiSettlement.HospitalDays),
        ["开始日期"] = nameof(YuehaiSettlement.StartDate),
        ["结束日期"] = nameof(YuehaiSettlement.EndDate),
        ["结算时间"] = nameof(YuehaiSettlement.SettlementTime),
        ["住院主诊断名称"] = nameof(YuehaiSettlement.PrimaryDiagnosisName),
        ["入院科室名称"] = nameof(YuehaiSettlement.AdmissionDeptName),
        ["出院科室名称"] = nameof(YuehaiSettlement.DischargeDeptName),
        ["人员参保关系ID"] = nameof(YuehaiSettlement.InsuranceRelationId),
        ["参保所属医保区划"] = nameof(YuehaiSettlement.InsuranceRegion),
        ["险种类型1"] = nameof(YuehaiSettlement.InsuranceType1),
        ["INSUTYPE"] = nameof(YuehaiSettlement.InsuType),
        ["支付地点类别1"] = nameof(YuehaiSettlement.PaymentLocationType1),
        ["支付地点类别"] = nameof(YuehaiSettlement.PaymentLocationType),
        ["医疗类别1"] = nameof(YuehaiSettlement.MedicalCategory1),
        ["医疗类别"] = nameof(YuehaiSettlement.MedicalCategory),
        ["录入方式"] = nameof(YuehaiSettlement.EntryMode),
        ["数据分割"] = nameof(YuehaiSettlement.DataSplit),
        ["费用明细流水号"] = nameof(YuehaiSettlement.FeeDetailSerialNo),
        ["费用发生时间"] = nameof(YuehaiSettlement.FeeOccurrenceTime),
        ["数量"] = nameof(YuehaiSettlement.Quantity),
        ["单价"] = nameof(YuehaiSettlement.UnitPrice),
        ["明细项目费用总额"] = nameof(YuehaiSettlement.FeeDetailTotalAmount),
        ["定价上限金额"] = nameof(YuehaiSettlement.PricingCapAmount),
        ["自付比例"] = nameof(YuehaiSettlement.SelfPayRatio),
        ["先支付类型"] = nameof(YuehaiSettlement.PrePaymentType),
        ["全自费金额"] = nameof(YuehaiSettlement.FullSelfPayAmount),
        ["超限价金额"] = nameof(YuehaiSettlement.OverLimitAmount),
        ["先行自付金额"] = nameof(YuehaiSettlement.AdvanceSelfPayAmount),
        ["符合范围金额"] = nameof(YuehaiSettlement.InScopeAmount),
        ["公务员床位费金额"] = nameof(YuehaiSettlement.CivilServantBedAmount),
        ["医院减免金额"] = nameof(YuehaiSettlement.HospitalDiscountAmount),
        ["医院垫付金额"] = nameof(YuehaiSettlement.HospitalAdvanceAmount),
        ["收费项目等级"] = nameof(YuehaiSettlement.ChargeItemLevel),
        ["医保目录编码"] = nameof(YuehaiSettlement.InsuranceCatalogCode),
        ["医保目录名称"] = nameof(YuehaiSettlement.InsuranceCatalogName),
        ["目录类别"] = nameof(YuehaiSettlement.CatalogCategory),
        ["医疗目录编码"] = nameof(YuehaiSettlement.MedicalCatalogCode),
        ["医药机构目录编码"] = nameof(YuehaiSettlement.InstitutionCatalogCode),
        ["医药机构目录名称"] = nameof(YuehaiSettlement.InstitutionCatalogName),
        ["医疗收费项目类别1"] = nameof(YuehaiSettlement.MedicalChargeCategory1),
        ["医疗收费项目类别"] = nameof(YuehaiSettlement.MedicalChargeCategory),
        ["规格"] = nameof(YuehaiSettlement.Specification),
        ["剂型名称"] = nameof(YuehaiSettlement.DosageFormName),
        ["开单科室编码"] = nameof(YuehaiSettlement.OrderingDeptCode),
        ["开单科室名称"] = nameof(YuehaiSettlement.OrderingDeptName),
        ["开单医师代码"] = nameof(YuehaiSettlement.OrderingDoctorCode),
        ["开单医师姓名"] = nameof(YuehaiSettlement.OrderingDoctorName),
        ["受单科室编码"] = nameof(YuehaiSettlement.ReceivingDeptCode),
        ["受单科室名称"] = nameof(YuehaiSettlement.ReceivingDeptName),
        ["受单医师代码"] = nameof(YuehaiSettlement.ReceivingDoctorCode),
        ["受单医师姓名"] = nameof(YuehaiSettlement.ReceivingDoctorName),
    };

    private readonly ConstructorInfo? _constructor;
    private readonly PropertyInfo[] _properties;

    public static void Register()
    {
        SqlMapper.SetTypeMap(typeof(YuehaiSettlement), new YuehaiSettlementTypeMap(typeof(YuehaiSettlement)));
    }

    private YuehaiSettlementTypeMap(Type type)
    {
        _properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        _constructor = type.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();
    }

    public ConstructorInfo? Constructor => _constructor;

    public ConstructorInfo? FindConstructor(string[] names, Type[] types)
    {
        return _constructor;
    }

    public ConstructorInfo? FindExplicitConstructor()
    {
        return _constructor;
    }

    public SqlMapper.IMemberMap GetConstructorParameter(ConstructorInfo constructor, string columnName)
    {
        var propertyName = ColumnMappings.TryGetValue(columnName, out var mappedName)
            ? mappedName
            : columnName;

        var match = constructor.GetParameters()
            .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

        return match != null ? new YuehaiSettlementMemberMap(columnName, match) : null!;
    }

    public SqlMapper.IMemberMap GetMember(string columnName)
    {
        var propertyName = ColumnMappings.TryGetValue(columnName, out var mappedName)
            ? mappedName
            : columnName;

        var property = _properties.FirstOrDefault(p =>
            string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

        return property != null ? new YuehaiSettlementMemberMap(columnName, property) : null!;
    }
}

/// <summary>
/// 简单的成员映射实现
/// </summary>
internal sealed class YuehaiSettlementMemberMap : SqlMapper.IMemberMap
{
    private readonly string _columnName;
    private readonly PropertyInfo? _property;
    private readonly ParameterInfo? _parameter;

    public YuehaiSettlementMemberMap(string columnName, PropertyInfo property)
    {
        _columnName = columnName;
        _property = property;
    }

    public YuehaiSettlementMemberMap(string columnName, ParameterInfo parameter)
    {
        _columnName = columnName;
        _parameter = parameter;
    }

    public string ColumnName => _columnName;
    public PropertyInfo? Property => _property;
    public FieldInfo? Field => null;
    public ParameterInfo? Parameter => _parameter;
    public Type Type => _property?.PropertyType ?? _parameter!.ParameterType;
    public Type MemberType => _property?.PropertyType ?? _parameter!.ParameterType;
}
