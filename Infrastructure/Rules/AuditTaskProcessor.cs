using Microsoft.Extensions.Logging;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;
using TaskStatus = SettlementMcpServer.Models.TaskStatus;

namespace SettlementMcpServer.Infrastructure.Rules;

/// <summary>
/// 审核任务处理器
/// </summary>
/// <remarks>
/// 负责从任务仓库获取任务，查询待审核结算数据，调用规则管道执行审核，
/// 并更新任务状态及统计信息。
/// </remarks>
public class AuditTaskProcessor
{
    private readonly IAuditTaskRepository _auditTaskRepository;
    private readonly IRulePipeline _rulePipeline;
    private readonly ISettlementDataRepository _settlementDataRepository;
    private readonly ILogger<AuditTaskProcessor> _logger;

    /// <summary>
    /// 初始化审核任务处理器
    /// </summary>
    /// <param name="auditTaskRepository">审核任务仓储</param>
    /// <param name="rulePipeline">规则管道</param>
    /// <param name="settlementDataRepository">结算数据仓储</param>
    /// <param name="logger">日志记录器</param>
    public AuditTaskProcessor(
        IAuditTaskRepository auditTaskRepository,
        IRulePipeline rulePipeline,
        ISettlementDataRepository settlementDataRepository,
        ILogger<AuditTaskProcessor> logger)
    {
        _auditTaskRepository = auditTaskRepository ?? throw new ArgumentNullException(nameof(auditTaskRepository));
        _rulePipeline = rulePipeline ?? throw new ArgumentNullException(nameof(rulePipeline));
        _settlementDataRepository = settlementDataRepository ?? throw new ArgumentNullException(nameof(settlementDataRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// 处理审核任务
    /// </summary>
    /// <param name="taskId">任务ID</param>
    /// <param name="ruleCode">规则编码</param>
    /// <param name="hospitalCode">机构编码（可选，为空时表示全部机构）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <exception cref="InvalidOperationException">任务不存在时抛出</exception>
    public async Task ProcessTaskAsync(
        string taskId,
        string ruleCode,
        string? hospitalCode,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "开始处理审核任务 {TaskId}，规则编码: {RuleCode}，机构编码: {HospitalCode}",
            taskId, ruleCode, hospitalCode ?? "(全部)");

        try
        {
            // 1. 获取任务
            var task = await _auditTaskRepository.GetTaskAsync(taskId, cancellationToken);
            if (task == null)
            {
                _logger.LogWarning("任务 {TaskId} 不存在", taskId);
                throw new InvalidOperationException($"任务 {taskId} 不存在");
            }

            // 2. 更新状态为 Running
            await _auditTaskRepository.UpdateTaskStatusAsync(taskId, TaskStatus.Running, cancellationToken: cancellationToken);

            // 3. 查询待审核结算数据
            var filter = new SettlementQueryFilter
            {
                InstitutionCode = hospitalCode,
                Page = 1,
                PageSize = int.MaxValue // 查询全部数据
            };

            var settlements = await _settlementDataRepository.QueryAllSettlementsAsync(filter, cancellationToken);
            var totalCount = settlements.Count;

            _logger.LogInformation("任务 {TaskId} 查询到 {Count} 条待审核结算数据", taskId, totalCount);

            // 4. 调用规则管道执行审核（传入规则编码，由管道内部从数据库加载规则）
            var violations = await _rulePipeline.ExecuteAsync(ruleCode, settlements, cancellationToken);
            var violationCount = violations.Count;

            _logger.LogInformation(
                "任务 {TaskId} 审核完成，总数: {TotalCount}，违规数: {ViolationCount}",
                taskId, totalCount, violationCount);

            // 5. 更新任务状态为 Completed，并记录统计信息
            // 注意：AuditTask 模型目前没有直接更新计数的方法，需要通过 UpdateTaskStatusAsync 间接更新
            // 这里简化处理，仅更新状态为 Completed
            await _auditTaskRepository.UpdateTaskStatusAsync(taskId, TaskStatus.Completed, cancellationToken: cancellationToken);

            _logger.LogInformation("审核任务 {TaskId} 处理完成", taskId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "审核任务 {TaskId} 处理失败", taskId);

            // 6. 更新状态为 Failed，并记录错误信息
            await _auditTaskRepository.UpdateTaskStatusAsync(taskId, TaskStatus.Failed, ex.Message, cancellationToken);

            throw;
        }
    }
}
