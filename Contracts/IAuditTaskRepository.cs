using SettlementMcpServer.Models;
// 使用别名避免与 System.Threading.Tasks.TaskStatus 冲突
using TaskStatus = SettlementMcpServer.Models.TaskStatus;

namespace SettlementMcpServer.Contracts;

/// <summary>
/// 审核任务仓储接口
/// </summary>
public interface IAuditTaskRepository
{
    /// <summary>
    /// 保存审核任务
    /// </summary>
    /// <param name="task">审核任务实体</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task SaveTaskAsync(AuditTask task, CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新任务状态及错误信息
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <param name="status">新状态</param>
    /// <param name="errorMessage">错误信息（可选）</param>
    /// <param name="cancellationToken">取消令牌</param>
    Task UpdateTaskStatusAsync(string taskId, TaskStatus status, string? errorMessage = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据任务ID获取任务详情
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>审核任务实体，不存在时返回 null</returns>
    Task<AuditTask?> GetTaskAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据规则名称查询关联的任务列表
    /// </summary>
    /// <param name="ruleName">规则名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>该规则名称下的任务列表</returns>
    Task<IReadOnlyList<AuditTask>> GetTasksByRuleNameAsync(string ruleName, CancellationToken cancellationToken = default);
}
