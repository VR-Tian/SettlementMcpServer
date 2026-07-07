using Microsoft.Extensions.Logging;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;
using SettlementMcpServer.Models.Rules;

namespace SettlementMcpServer.Infrastructure.Rules;

/// <summary>
/// 规则管道实现
/// </summary>
public class RulePipeline : IRulePipeline
{
    private readonly IRuleLoader _ruleLoader;
    private readonly IEnumerable<IRuleExecutor> _executors;
    private readonly IAuditResultRepository _auditResultRepository;
    private readonly IRuleCombinationExecutor _ruleCombinationExecutor;
    private readonly ILogger<RulePipeline> _logger;

    public RulePipeline(
        IRuleLoader ruleLoader,
        IEnumerable<IRuleExecutor> executors,
        IAuditResultRepository auditResultRepository,
        IRuleCombinationExecutor ruleCombinationExecutor,
        ILogger<RulePipeline> logger)
    {
        _ruleLoader = ruleLoader;
        _executors = executors;
        _auditResultRepository = auditResultRepository;
        _ruleCombinationExecutor = ruleCombinationExecutor;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuleViolation>> ExecuteAsync(
        string ruleName,
        IEnumerable<Settlement> settlements,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始执行规则管道，规则名称: {RuleName}", ruleName);

        var ruleSet = await _ruleLoader.LoadRuleSetAsync(ruleName, cancellationToken);

        var executor = _executors.FirstOrDefault(e => e.SupportedCategory == ruleSet.Category);
        if (executor is null)
        {
            throw new InvalidOperationException(
                $"找不到支持规则类别 {ruleSet.Category} 的执行器");
        }

        _logger.LogInformation(
            "已匹配执行器 {ExecutorType}，规则类别: {Category}",
            executor.GetType().Name,
            ruleSet.Category);

        var violations = await executor.ExecuteAsync(ruleSet, settlements, cancellationToken);

        _logger.LogInformation(
            "规则管道执行完成，规则名称: {RuleName}，发现违规数量: {ViolationCount}",
            ruleName,
            violations.Count);

        // 将违规结果转换为审核结果并持久化
        if (violations.Count > 0)
        {
            var taskId = Guid.NewGuid().ToString("N");
            var auditResults = violations.Select(v => MapToAuditResult(v, taskId)).ToList();

            await _auditResultRepository.SaveAuditResultsAsync(auditResults, cancellationToken);

            _logger.LogInformation(
                "已将 {Count} 条审核结果保存到 DuckDB，任务ID: {TaskId}",
                auditResults.Count,
                taskId);
        }

        return violations;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RuleViolation>> ExecuteCombinationAsync(
        IReadOnlyList<string> ruleNames,
        RuleCombination combination,
        IEnumerable<Settlement> settlements,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation(
            "开始执行规则组合管道，组合编码: {CombinationCode}，组合名称: {CombinationName}",
            combination.CombinationCode,
            combination.CombinationName);

        // 根据规则名称列表加载规则集
        var ruleSets = new List<IRuleSet>();
        foreach (var ruleName in ruleNames)
        {
            var ruleSet = await _ruleLoader.LoadRuleSetAsync(ruleName, cancellationToken);
            ruleSets.Add(ruleSet);
        }

        var violations = await _ruleCombinationExecutor.ExecuteAsync(ruleSets, combination, settlements, cancellationToken);

        _logger.LogInformation(
            "规则组合管道执行完成，组合编码: {CombinationCode}，发现违规数量: {ViolationCount}",
            combination.CombinationCode,
            violations.Count);

        // 将违规结果转换为审核结果并持久化
        if (violations.Count > 0)
        {
            var taskId = Guid.NewGuid().ToString("N");
            var auditResults = violations.Select(v => MapToAuditResult(v, taskId)).ToList();

            await _auditResultRepository.SaveAuditResultsAsync(auditResults, cancellationToken);

            _logger.LogInformation(
                "已将 {Count} 条审核结果保存到 DuckDB，任务ID: {TaskId}",
                auditResults.Count,
                taskId);
        }

        return violations;
    }

    /// <summary>
    /// 将规则违规结果映射为统一审核结果
    /// </summary>
    /// <param name="violation">规则违规结果</param>
    /// <param name="taskId">审核任务ID</param>
    /// <returns>统一审核结果</returns>
    private AuditResult MapToAuditResult(RuleViolation violation, string taskId)
    {
        return new AuditResult
        {
            Id = Guid.NewGuid().ToString(),
            TaskId = taskId,
            RuleName = violation.RuleName,
            RuleType = violation.RuleCategory.ToString(),
            PersonnelNo = violation.PersonnelNo,
            InstitutionCode = violation.InstitutionCode,
            InstitutionName = violation.InstitutionName,
            ViolationItemCode = violation.ViolationItemCode,
            ViolationItemName = violation.ViolationItemName,
            ViolationQuantity = violation.ViolationQuantity,
            ViolationUnitPrice = violation.ViolationUnitPrice,
            ViolationAmount = violation.ViolationAmount,
            FeeOccurrenceDate = violation.FeeOccurrenceDate,
            AuditTime = DateTime.Now,
            ReceivingDeptCode = violation.ReceivingDeptCode,
            ReceivingDeptName = violation.ReceivingDeptName,
            PromptMessage = violation.PromptMessage,
            Status = AuditStatus.Pending,
            CreatedAt = DateTime.Now
        };
    }
}
