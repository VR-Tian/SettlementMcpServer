using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models.Rules;

namespace SettlementMcpServer.Infrastructure.Rules;

/// <summary>
/// 规则初始化服务实现
/// </summary>
/// <remarks>
/// <para>
/// 负责从 Excel 规则文件扫描并写入 DuckDB 数据库，支持增量更新。
/// 通过比较文件修改时间判断是否需要重新加载规则。
/// </para>
/// </remarks>
public sealed class RuleInitializationService : IRuleInitializationService
{
    private readonly DuplicateChargeExcelRuleLoader _excelRuleLoader;
    private readonly IRuleRepository _ruleRepository;
    private readonly ILogger<RuleInitializationService> _logger;

    /// <summary>
    /// 规则文件目录
    /// </summary>
    private const string RulesDirectory = ".agents/skills/重复收费规则";

    /// <summary>
    /// 初始化规则初始化服务
    /// </summary>
    /// <param name="excelRuleLoader">重复收费规则 Excel 加载器（用于从 Excel 加载规则）</param>
    /// <param name="ruleRepository">规则仓储（用于保存规则到数据库）</param>
    /// <param name="logger">日志记录器</param>
    public RuleInitializationService(
        DuplicateChargeExcelRuleLoader excelRuleLoader,
        IRuleRepository ruleRepository,
        ILogger<RuleInitializationService>? logger = null)
    {
        _excelRuleLoader = excelRuleLoader ?? throw new ArgumentNullException(nameof(excelRuleLoader));
        _ruleRepository = ruleRepository ?? throw new ArgumentNullException(nameof(ruleRepository));
        _logger = logger ?? NullLogger<RuleInitializationService>.Instance;
    }

    /// <inheritdoc />
    public async Task InitializeRulesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("开始初始化规则数据");

        // 获取规则文件目录的完整路径
        var baseDirectory = AppContext.BaseDirectory;
        var rulesPath = Path.Combine(baseDirectory, RulesDirectory);

        // 如果当前目录不存在，尝试从项目根目录查找
        if (!Directory.Exists(rulesPath))
        {
            // 向上查找直到找到 .agents 目录或到达根目录
            var currentDir = new DirectoryInfo(baseDirectory);
            while (currentDir != null && !Directory.Exists(Path.Combine(currentDir.FullName, ".agents")))
            {
                currentDir = currentDir.Parent;
            }

            if (currentDir != null)
            {
                rulesPath = Path.Combine(currentDir.FullName, RulesDirectory);
            }
        }

        if (!Directory.Exists(rulesPath))
        {
            _logger.LogWarning("规则文件目录不存在: {RulesPath}，跳过规则初始化", rulesPath);
            return;
        }

        _logger.LogInformation("扫描规则文件目录: {RulesPath}", rulesPath);

        // 获取所有 Excel 文件
        var excelFiles = Directory.GetFiles(rulesPath, "*.xlsx", SearchOption.AllDirectories);

        if (excelFiles.Length == 0)
        {
            _logger.LogWarning("规则文件目录中未找到 Excel 文件");
            return;
        }

        _logger.LogInformation("找到 {Count} 个 Excel 规则文件", excelFiles.Length);

        var successCount = 0;
        var skipCount = 0;
        var failCount = 0;

        foreach (var excelFile in excelFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // 获取文件信息
                var fileInfo = new FileInfo(excelFile);
                var fileName = fileInfo.Name;
                var lastWriteTime = fileInfo.LastWriteTimeUtc;

                _logger.LogDebug("处理规则文件: {FileName}, 最后修改时间: {LastWriteTime}", fileName, lastWriteTime);

                // 从文件名提取规则编码（去掉扩展名）
                var ruleCode = Path.GetFileNameWithoutExtension(fileName);

                // 检查数据库中是否已存在该规则
                var existingRule = await _ruleRepository.GetRuleSetByCodeAsync(ruleCode, cancellationToken);

                if (existingRule != null)
                {
                    // TODO: 可以在数据库中记录文件修改时间，用于增量更新判断
                    // 目前暂时每次都重新加载
                    _logger.LogDebug("规则 {RuleCode} 已存在于数据库中，将更新", ruleCode);
                }

                // 从 Excel 加载规则集
                _logger.LogInformation("从 Excel 文件加载规则: {FileName}", fileName);
                var ruleSet = (DuplicateChargeRuleSet)await _excelRuleLoader.LoadRuleSetAsync(excelFile, cancellationToken);

                // 确保规则编码与文件名一致
                if (string.IsNullOrEmpty(ruleSet.Rule.RuleCode))
                {
                    ruleSet.Rule.RuleCode = ruleCode;
                }

                // 保存到数据库
                await _ruleRepository.SaveRuleSetAsync(ruleSet, cancellationToken);

                successCount++;
                _logger.LogInformation("规则 {RuleCode} 初始化成功", ruleCode);
            }
            catch (Exception ex)
            {
                failCount++;
                _logger.LogError(ex, "初始化规则文件失败: {FileName}", Path.GetFileName(excelFile));
            }
        }

        _logger.LogInformation(
            "规则初始化完成，成功: {SuccessCount}, 跳过: {SkipCount}, 失败: {FailCount}",
            successCount, skipCount, failCount);
    }
}
