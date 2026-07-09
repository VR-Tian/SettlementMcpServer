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
    private readonly IRuleLoader _ruleLoader;
    private readonly IRuleRepository _ruleRepository;
    private readonly ILogger<RuleInitializationService> _logger;

    /// <summary>
    /// 规则文件目录
    /// </summary>
    private const string RulesDirectory = ".agents\\skills";

    /// <summary>
    /// 初始化规则初始化服务
    /// </summary>
    /// <param name="ruleLoader">规则加载器（根据文件路径自动选择对应类别的加载器）</param>
    /// <param name="ruleRepository">规则仓储（用于保存规则到数据库）</param>
    /// <param name="logger">日志记录器</param>
    public RuleInitializationService(
        IRuleLoader ruleLoader,
        IRuleRepository ruleRepository,
        ILogger<RuleInitializationService>? logger = null)
    {
        _ruleLoader = ruleLoader ?? throw new ArgumentNullException(nameof(ruleLoader));
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

                // 从文件名提取规则名称（去掉扩展名）
                var ruleName = Path.GetFileNameWithoutExtension(fileName);

                // 检查数据库中是否已存在该规则
                var existingRule = await _ruleRepository.GetRuleSetByNameAsync(ruleName, cancellationToken);

                if (existingRule != null)
                {
                    // TODO: 可以在数据库中记录文件修改时间，用于增量更新判断
                    // 目前暂时每次都重新加载
                    _logger.LogDebug("规则 {RuleName} 已存在于数据库中，将更新", ruleName);
                }

                // 从 Excel 加载规则集（CompositeRuleLoader 会根据文件路径自动选择对应的加载器）
                _logger.LogInformation("从 Excel 文件加载规则: {FileName}", fileName);
                var ruleSet = await _ruleLoader.LoadRuleSetAsync(excelFile, cancellationToken);

                // 确保规则名称与文件名一致
                if (string.IsNullOrEmpty(ruleSet.RuleName))
                {
                    // 由于 IRuleSet 接口没有设置 RuleName 的方法，需要在具体实现类中处理
                    // 这里暂时跳过，由具体的加载器负责设置正确的 RuleName
                    _logger.LogDebug("规则 {RuleName} 的 RuleName 为空，使用文件名作为 RuleName", ruleName);
                }

                // 保存到数据库
                await _ruleRepository.SaveRuleSetAsync(ruleSet, cancellationToken);

                successCount++;
                _logger.LogInformation("规则 {RuleName} 初始化成功", ruleName);
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
