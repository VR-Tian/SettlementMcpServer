using SettlementMcpServer.Models;

namespace SettlementMcpServer.Contracts;

/// <summary>
/// 审核结果仓储接口
/// </summary>
public interface IAuditResultRepository
{
    /// <summary>
    /// 批量保存审核结果
    /// </summary>
    Task SaveAuditResultsAsync(IEnumerable<AuditResult> results, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据任务ID查询审核结果
    /// </summary>
    Task<IReadOnlyList<AuditResult>> GetAuditResultsByTaskIdAsync(string taskId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 根据规则名称查询审核结果
    /// </summary>
    Task<IReadOnlyList<AuditResult>> GetAuditResultsByRuleNameAsync(string ruleName, CancellationToken cancellationToken = default);
}
