using Dapper;
using SettlementMcpServer.Models;

namespace SettlementMcpServer.Infrastructure;

/// <summary>
/// 自定义 Dapper 类型映射器 - 将 Oracle 中文列名映射到英文属性名
/// </summary>
/// <remarks>
/// <para>
/// <b>为什么需要自定义类型映射？</b>
/// </para>
/// <para>
/// Dapper 默认通过列名和属性名的字符串匹配（忽略大小写）完成数据映射。
/// 但本项目的 Oracle 表使用中文列名（如"病案号"、"医院编码"），而 C# 模型类使用英文属性名
/// （如 MedicalRecordNo、HospitalCode），两者无法直接匹配。
/// </para>
/// <para>
/// <b>两种解决方案对比：</b>
/// </para>
/// <list type="bullet">
///   <item><description>方案 A：在 SQL 中为每个列写 AS 别名（SELECT 病案号 AS MedicalRecordNo...），但 50 个字段会导致 SQL 极其冗长。</description></item>
///   <item><description>方案 B（本方案）：实现 <see cref="SqlMapper.ITypeMap"/> 接口，通过字典维护列名到属性名的映射关系，SQL 保持 SELECT * 即可。</description></item>
/// </list>
/// <para>
/// 选择方案 B 的理由：SQL 更简洁可维护，映射关系集中在一个位置管理。
/// </para>
/// <para>
/// <b>注册时机：</b>
/// 类型映射必须在 Dapper 首次查询 <see cref="Models.AuditedResult"/> 之前注册。
/// 推荐在 DI 注册阶段调用 <see cref="Register"/> 方法，确保映射在应用启动时完成。
/// </para>
/// </remarks>
internal sealed class AuditedResultTypeMap : DapperTypeMapBase
{
    /// <summary>
    /// 列名 → 属性名映射字典
    /// </summary>
    /// <remarks>
    /// <para>
    /// 键为 Oracle 表中的原始列名（中文），值为 <see cref="Models.AuditedResult"/> 类中的对应属性名。
    /// 使用 <see cref="StringComparer.OrdinalIgnoreCase"/> 实现大小写不敏感的键匹配。
    /// </para>
    /// <para>
    /// 新增字段时只需在此字典中添加一行映射，无需修改 SQL 或其他代码。
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, string> _columnMappings = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = nameof(AuditedResult.Id),
        ["负面清单行为类别"] = nameof(AuditedResult.NegativeListCategory),
        ["行为释义"] = nameof(AuditedResult.BehaviorDescription),
        ["相关项目"] = nameof(AuditedResult.RelatedItems),
        ["违规说明"] = nameof(AuditedResult.ViolationExplanation),
        ["政策依据"] = nameof(AuditedResult.PolicyBasis),
        ["病案号"] = nameof(AuditedResult.MedicalRecordNo),
        ["医院编码"] = nameof(AuditedResult.HospitalCode),
        ["医院名称"] = nameof(AuditedResult.HospitalName),
        ["医院级别"] = nameof(AuditedResult.HospitalLevel),
        ["出院科室"] = nameof(AuditedResult.DischargeDepartment),
        ["业务号"] = nameof(AuditedResult.BusinessNo),
        ["入院日期"] = nameof(AuditedResult.AdmissionDate),
        ["出院日期"] = nameof(AuditedResult.DischargeDate),
        ["结算日期"] = nameof(AuditedResult.SettlementDate),
        ["住院日"] = nameof(AuditedResult.HospitalDays),
        ["出院诊断编码"] = nameof(AuditedResult.DischargeDiagnosisCode),
        ["出院诊断"] = nameof(AuditedResult.DischargeDiagnosis),
        ["其他诊断"] = nameof(AuditedResult.OtherDiagnosis),
        ["其他诊断名称"] = nameof(AuditedResult.OtherDiagnosisName),
        ["参保人号"] = nameof(AuditedResult.InsuredNo),
        ["参保人"] = nameof(AuditedResult.InsuredName),
        ["出生日期"] = nameof(AuditedResult.BirthDate),
        ["年龄"] = nameof(AuditedResult.Age),
        ["性别"] = nameof(AuditedResult.Gender),
        ["就医方式"] = nameof(AuditedResult.VisitType),
        ["待遇类型"] = nameof(AuditedResult.BenefitType),
        ["参保类型"] = nameof(AuditedResult.InsuranceType),
        ["人员类别"] = nameof(AuditedResult.PersonnelCategory),
        ["病例总金额"] = nameof(AuditedResult.CaseTotalAmount),
        ["医保金额"] = nameof(AuditedResult.InsuranceAmount),
        ["明细id"] = nameof(AuditedResult.DetailId),
        ["项目日期"] = nameof(AuditedResult.ItemDate),
        ["项目编码"] = nameof(AuditedResult.ItemCode),
        ["项目名称"] = nameof(AuditedResult.ItemName),
        ["医院项目编码"] = nameof(AuditedResult.HospitalItemCode),
        ["医院项目名称"] = nameof(AuditedResult.HospitalItemName),
        ["单价"] = nameof(AuditedResult.UnitPrice),
        ["数量"] = nameof(AuditedResult.Quantity),
        ["金额"] = nameof(AuditedResult.Amount),
        ["医保内金额"] = nameof(AuditedResult.InInsuranceAmount),
        ["科室"] = nameof(AuditedResult.Department),
        ["医生"] = nameof(AuditedResult.Doctor),
        ["规则编码"] = nameof(AuditedResult.RuleCode),
        ["规则名称"] = nameof(AuditedResult.RuleName),
        ["理由说明"] = nameof(AuditedResult.ReasonExplanation),
        ["涉及明细金额"] = nameof(AuditedResult.InvolvedDetailAmount),
        ["单据类别"] = nameof(AuditedResult.DocumentCategory),
        ["清单编码"] = nameof(AuditedResult.ListCode),
    };

    /// <summary>
    /// 列名 → 属性名映射字典（由基类要求实现）
    /// </summary>
    protected override Dictionary<string, string> ColumnMappings => _columnMappings;

    /// <summary>
    /// 向 Dapper 注册此类型映射器
    /// </summary>
    /// <remarks>
    /// <para>
    /// 此方法必须在 Dapper 首次查询 <see cref="AuditedResult"/> 之前调用。
    /// 推荐在应用启动阶段（DI 注册时）调用，而非在仓储的静态构造函数中。
    /// </para>
    /// <para>
    /// <b>为什么不在仓储静态构造函数中注册？</b>
    /// </para>
    /// <list type="bullet">
    ///   <item><description>静态构造函数延迟到类型首次被访问时才执行，如果仓储未被实例化则映射未注册。</description></item>
    ///   <item><description>在 DI 注册阶段调用可确保映射在应用启动时立即生效，避免延迟初始化带来的不确定性。</description></item>
    ///   <item><description>符合"显式优于隐式"的设计原则——读者一眼就能看出类型映射在哪里注册。</description></item>
    /// </list>
    /// </remarks>
    public static void Register()
    {
        SqlMapper.SetTypeMap(typeof(AuditedResult), new AuditedResultTypeMap(typeof(AuditedResult)));
    }

    /// <summary>
    /// 初始化类型映射器实例
    /// </summary>
    /// <param name="type">目标类型（应为 <see cref="Models.AuditedResult"/>）</param>
    private AuditedResultTypeMap(Type type) : base(type)
    {
    }
}
