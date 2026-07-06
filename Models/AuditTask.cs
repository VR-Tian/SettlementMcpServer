namespace SettlementMcpServer.Models;

/// <summary>
/// 审核任务模型
/// </summary>
public class AuditTask
{
    /// <summary>任务ID</summary>
    public string TaskId { get; set; } = string.Empty;

    /// <summary>规则编码</summary>
    public string RuleCode { get; set; } = string.Empty;

    /// <summary>机构编码（可选，为空时表示全部机构）</summary>
    public string? HospitalCode { get; set; }

    /// <summary>任务状态</summary>
    public TaskStatus Status { get; set; }

    /// <summary>待审核结算数据总数</summary>
    public int TotalCount { get; set; }

    /// <summary>已处理结算数据数量</summary>
    public int ProcessedCount { get; set; }

    /// <summary>违规数量</summary>
    public int ViolationCount { get; set; }

    /// <summary>任务创建时间</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>任务完成时间</summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>错误信息（任务失败时记录）</summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// 审核任务状态枚举
/// </summary>
public enum TaskStatus
{
    /// <summary>待处理</summary>
    Pending = 0,

    /// <summary>执行中</summary>
    Running = 1,

    /// <summary>已完成</summary>
    Completed = 2,

    /// <summary>执行失败</summary>
    Failed = 3
}
