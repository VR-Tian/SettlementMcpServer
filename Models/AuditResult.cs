namespace SettlementMcpServer.Models;

/// <summary>
/// 统一审核结果模型
/// </summary>
public class AuditResult
{
    /// <summary>主键</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>审核任务ID</summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>规则名称（来源于文件名）</summary>
    public string RuleName { get; set; } = string.Empty;

    /// <summary>规则类型</summary>
    public string RuleType { get; set; } = string.Empty;

    /// <summary>人员编号</summary>
    public string PersonnelNo { get; set; } = string.Empty;

    /// <summary>人员姓名</summary>
    public string? PersonnelName { get; set; }

    /// <summary>机构编码</summary>
    public string InstitutionCode { get; set; } = string.Empty;

    /// <summary>机构名称</summary>
    public string? InstitutionName { get; set; }

    /// <summary>违规项目编码</summary>
    public string ViolationItemCode { get; set; } = string.Empty;

    /// <summary>违规项目名称</summary>
    public string? ViolationItemName { get; set; }

    /// <summary>违规数量</summary>
    public decimal? ViolationQuantity { get; set; }

    /// <summary>违规单价</summary>
    public decimal? ViolationUnitPrice { get; set; }

    /// <summary>违规金额</summary>
    public decimal? ViolationAmount { get; set; }

    /// <summary>费用发生日期</summary>
    public string FeeOccurrenceDate { get; set; } = string.Empty;

    /// <summary>审核时间</summary>
    public DateTime AuditTime { get; set; }

    /// <summary>受单科室编码</summary>
    public string? ReceivingDeptCode { get; set; }

    /// <summary>受单科室名称</summary>
    public string? ReceivingDeptName { get; set; }

    /// <summary>提示信息</summary>
    public string PromptMessage { get; set; } = string.Empty;

    /// <summary>处理状态</summary>
    public AuditStatus Status { get; set; }

    /// <summary>处理备注</summary>
    public string? HandleRemark { get; set; }

    /// <summary>创建时间</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 审核状态枚举
/// </summary>
public enum AuditStatus
{
    /// <summary>待处理</summary>
    Pending = 0,

    /// <summary>处理中</summary>
    Processing = 1,

    /// <summary>已处理</summary>
    Resolved = 2,

    /// <summary>已忽略</summary>
    Ignored = 3
}
