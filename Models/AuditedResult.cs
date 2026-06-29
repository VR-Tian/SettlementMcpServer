namespace SettlementMcpServer.Models;

/// <summary>
/// 审核结果查询条件
/// </summary>
/// <remarks>
/// 用于封装 <see cref="AuditedResult"/> 查询的所有可选过滤参数。
/// 所有字段均为可选，不传入时仓储层将忽略对应 WHERE 条件。
/// </remarks>
public class AuditedResultQueryFilter
{
    /// <summary>病案号（对应表字段: 病案号）</summary>
    /// <remarks>精确匹配，为空时忽略此条件。</remarks>
    public string? MedicalRecordNo { get; set; }

    /// <summary>医院编码（对应表字段: 医院编码）</summary>
    /// <remarks>精确匹配，为空时忽略此条件。</remarks>
    public string? HospitalCode { get; set; }

    /// <summary>参保人号（对应表字段: 参保人号）</summary>
    /// <remarks>精确匹配，为空时忽略此条件。</remarks>
    public string? InsuredNo { get; set; }

    /// <summary>规则编码（对应表字段: 规则编码）</summary>
    /// <remarks>精确匹配，为空时忽略此条件。</remarks>
    public string? RuleCode { get; set; }

    /// <summary>
    /// 页码，从 1 开始
    /// </summary>
    /// <remarks>
    /// 默认 1。与 <see cref="PageSize"/> 配合使用实现分页查询。
    /// 小于 1 时自动回退到 1。
    /// </remarks>
    public int Page { get; set; } = 1;

    /// <summary>
    /// 每页条数
    /// </summary>
    /// <remarks>
    /// 默认 100，最大 500。用于控制单次查询返回的数据量，
    /// 避免超出 MCP 上下文长度限制。
    /// </remarks>
    public int PageSize { get; set; } = 100;
}

/// <summary>
/// 分页查询结果元数据
/// </summary>
/// <remarks>
/// <para>
/// 由步骤 1（<c>GetAuditedResultCountAsync</c>）返回，包含当前查询条件下的总记录数、
/// 总页数、当前页码等信息，客户端据此计算需要循环请求的页数。
/// </para>
/// <para>
/// <b>使用示例：</b>
/// <code>
/// // 步骤 1：获取总数
/// var pagination = await GetAuditedResultCountAsync(hospitalCode: "H001");
/// // pagination.TotalCount = 1250, pagination.TotalPages = 13 (pageSize=100)
///
/// // 步骤 2：循环请求每一页
/// for (int page = 1; page &lt;= pagination.TotalPages; page++)
/// {
///     var results = await QueryAuditedResultsAsync(hospitalCode: "H001", page: page, pageSize: 100);
///     // 处理 results...
/// }
/// </code>
/// </para>
/// </remarks>
public record AuditedResultPagination
{
    /// <summary>
    /// 符合查询条件的总记录数
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 每页条数
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// 总页数
    /// </summary>
    /// <remarks>计算公式: (TotalCount + PageSize - 1) / PageSize</remarks>
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;

    /// <summary>
    /// 当前页码（用于确认请求的参数）
    /// </summary>
    public int CurrentPage { get; init; }

    /// <summary>
    /// 是否还有下一页
    /// </summary>
    public bool HasNextPage => CurrentPage < TotalPages;

    /// <summary>
    /// 是否还有上一页
    /// </summary>
    public bool HasPreviousPage => CurrentPage > 1;
}

/// <summary>
/// 医保审核数据结果 - 对应表 DW_AUDITED_RESULT_1464_24AND25
/// </summary>
/// <remarks>
/// <para>此类专供 Dapper ORM 映射使用，属性名通过 <see cref="Infrastructure.AuditedResultTypeMap"/> 映射到 Oracle 表中的中文列名。</para>
/// <para>所有引用类型属性均可为 null（对应数据库中的 NULL 值），数值类型属性使用 <c>decimal?</c> 以支持空值。</para>
/// </remarks>
public class AuditedResult
{
    /// <summary>主键 ID（对应表字段: id, VARCHAR2(50)）</summary>
    public string? Id { get; set; }

    /// <summary>负面清单行为类别（对应表字段: 负面清单行为类别, VARCHAR2(2500)）</summary>
    public string? NegativeListCategory { get; set; }

    /// <summary>行为释义（对应表字段: 行为释义, VARCHAR2(2500)）</summary>
    public string? BehaviorDescription { get; set; }

    /// <summary>相关项目（对应表字段: 相关项目, VARCHAR2(2500)）</summary>
    public string? RelatedItems { get; set; }

    /// <summary>违规说明（对应表字段: 违规说明, VARCHAR2(2500)）</summary>
    public string? ViolationExplanation { get; set; }

    /// <summary>政策依据（对应表字段: 政策依据, VARCHAR2(2500)）</summary>
    public string? PolicyBasis { get; set; }

    /// <summary>病案号（对应表字段: 病案号, NVARCHAR2(120)）</summary>
    public string? MedicalRecordNo { get; set; }

    /// <summary>医院编码（对应表字段: 医院编码, VARCHAR2(50)）</summary>
    public string? HospitalCode { get; set; }

    /// <summary>医院名称（对应表字段: 医院名称, VARCHAR2(600)）</summary>
    public string? HospitalName { get; set; }

    /// <summary>医院级别（对应表字段: 医院级别, VARCHAR2(36)）</summary>
    public string? HospitalLevel { get; set; }

    /// <summary>出院科室（对应表字段: 出院科室, VARCHAR2(300)）</summary>
    public string? DischargeDepartment { get; set; }

    /// <summary>业务号（对应表字段: 业务号, VARCHAR2(50)）</summary>
    public string? BusinessNo { get; set; }

    /// <summary>入院日期（对应表字段: 入院日期, VARCHAR2(10)）</summary>
    public string? AdmissionDate { get; set; }

    /// <summary>出院日期（对应表字段: 出院日期, VARCHAR2(10)）</summary>
    public string? DischargeDate { get; set; }

    /// <summary>结算日期（对应表字段: 结算日期, VARCHAR2(19)）</summary>
    public string? SettlementDate { get; set; }

    /// <summary>住院日（对应表字段: 住院日, NUMBER）</summary>
    public decimal? HospitalDays { get; set; }

    /// <summary>出院诊断编码（对应表字段: 出院诊断编码, VARCHAR2(200)）</summary>
    public string? DischargeDiagnosisCode { get; set; }

    /// <summary>出院诊断（对应表字段: 出院诊断, VARCHAR2(400)）</summary>
    public string? DischargeDiagnosis { get; set; }

    /// <summary>其他诊断（对应表字段: 其他诊断, VARCHAR2(3000)）</summary>
    public string? OtherDiagnosis { get; set; }

    /// <summary>其他诊断名称（对应表字段: 其他诊断名称, VARCHAR2(3900)）</summary>
    public string? OtherDiagnosisName { get; set; }

    /// <summary>参保人号（对应表字段: 参保人号, VARCHAR2(180)）</summary>
    public string? InsuredNo { get; set; }

    /// <summary>参保人姓名（对应表字段: 参保人, VARCHAR2(300)）</summary>
    public string? InsuredName { get; set; }

    /// <summary>出生日期（对应表字段: 出生日期, VARCHAR2(10)）</summary>
    public string? BirthDate { get; set; }

    /// <summary>年龄（对应表字段: 年龄, NUMBER(20,2)）</summary>
    public decimal? Age { get; set; }

    /// <summary>性别（对应表字段: 性别, VARCHAR2(6)）</summary>
    public string? Gender { get; set; }

    /// <summary>就医方式（对应表字段: 就医方式, VARCHAR2(36)）</summary>
    public string? VisitType { get; set; }

    /// <summary>待遇类型（对应表字段: 待遇类型, VARCHAR2(1)）</summary>
    public string? BenefitType { get; set; }

    /// <summary>参保类型（对应表字段: 参保类型, VARCHAR2(36)）</summary>
    public string? InsuranceType { get; set; }

    /// <summary>人员类别（对应表字段: 人员类别, VARCHAR2(36)）</summary>
    public string? PersonnelCategory { get; set; }

    /// <summary>病例总金额（对应表字段: 病例总金额, NUMBER(20,2)）</summary>
    public decimal? CaseTotalAmount { get; set; }

    /// <summary>医保金额（对应表字段: 医保金额, NUMBER(20,2)）</summary>
    public decimal? InsuranceAmount { get; set; }

    /// <summary>明细 ID（对应表字段: 明细id, VARCHAR2(50)）</summary>
    public string? DetailId { get; set; }

    /// <summary>项目日期（对应表字段: 项目日期, VARCHAR2(19)）</summary>
    public string? ItemDate { get; set; }

    /// <summary>项目编码（对应表字段: 项目编码, VARCHAR2(300)）</summary>
    public string? ItemCode { get; set; }

    /// <summary>项目名称（对应表字段: 项目名称, VARCHAR2(800)）</summary>
    public string? ItemName { get; set; }

    /// <summary>医院项目编码（对应表字段: 医院项目编码, VARCHAR2(800)）</summary>
    public string? HospitalItemCode { get; set; }

    /// <summary>医院项目名称（对应表字段: 医院项目名称, VARCHAR2(800)）</summary>
    public string? HospitalItemName { get; set; }

    /// <summary>单价（对应表字段: 单价, NUMBER）</summary>
    public decimal? UnitPrice { get; set; }

    /// <summary>数量（对应表字段: 数量, NUMBER）</summary>
    public decimal? Quantity { get; set; }

    /// <summary>金额（对应表字段: 金额, NUMBER）</summary>
    public decimal? Amount { get; set; }

    /// <summary>医保内金额（对应表字段: 医保内金额, NUMBER）</summary>
    public decimal? InInsuranceAmount { get; set; }

    /// <summary>科室（对应表字段: 科室, VARCHAR2(800)）</summary>
    public string? Department { get; set; }

    /// <summary>医生（对应表字段: 医生, VARCHAR2(300)）</summary>
    public string? Doctor { get; set; }

    /// <summary>规则编码（对应表字段: 规则编码, VARCHAR2(50)）</summary>
    public string? RuleCode { get; set; }

    /// <summary>规则名称（对应表字段: 规则名称, VARCHAR2(500)）</summary>
    public string? RuleName { get; set; }

    /// <summary>理由说明（对应表字段: 理由说明, VARCHAR2(3000)）</summary>
    public string? ReasonExplanation { get; set; }

    /// <summary>涉及明细金额（对应表字段: 涉及明细金额, NUMBER(18,2)）</summary>
    public decimal? InvolvedDetailAmount { get; set; }

    /// <summary>单据类别（对应表字段: 单据类别, CHAR(9)）</summary>
    public string? DocumentCategory { get; set; }

    /// <summary>清单编码（对应表字段: 清单编码, VARCHAR2(50)）</summary>
    public string? ListCode { get; set; }
}
