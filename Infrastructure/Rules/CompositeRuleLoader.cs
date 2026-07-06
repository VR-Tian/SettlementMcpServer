using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models.Rules;

namespace SettlementMcpServer.Infrastructure.Rules;

/// <summary>
/// 复合规则加载器
/// </summary>
/// <remarks>
/// <para>
/// 支持多种规则类别的加载，根据规则类别自动选择对应的加载器。
/// </para>
/// </remarks>
public sealed class CompositeRuleLoader : IRuleLoader
{
    private readonly IReadOnlyDictionary<RuleCategory, IRuleLoader> _loaders;
    private readonly ILogger<CompositeRuleLoader> _logger;

    /// <summary>
    /// 初始化复合规则加载器
    /// </summary>
    /// <param name="loaders">规则加载器集合</param>
    /// <param name="logger">日志记录器</param>
    public CompositeRuleLoader(
        IEnumerable<IRuleLoader> loaders,
        ILogger<CompositeRuleLoader>? logger = null)
    {
        _loaders = loaders.ToDictionary(l => l.SupportedCategory);
        _logger = logger ?? NullLogger<CompositeRuleLoader>.Instance;

        _logger.LogInformation("复合规则加载器初始化完成，支持 {Count} 种规则类别", _loaders.Count);
    }

    /// <inheritdoc />
    public RuleCategory SupportedCategory => throw new NotSupportedException("复合加载器不支持特定规则类别");

    /// <inheritdoc />
    public Task<IRuleSet> LoadRuleSetAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        _logger.LogInformation("开始加载规则集，文件路径: {FilePath}", filePath);

        // 根据文件路径推断规则类别
        var category = InferRuleCategory(filePath);
        
        if (!_loaders.TryGetValue(category, out var loader))
        {
            throw new InvalidOperationException($"不支持的规则类别: {category}");
        }

        return loader.LoadRuleSetAsync(filePath, cancellationToken);
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<IRuleSet>> LoadAllRuleSetsAsync(string directoryPath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(directoryPath);

        _logger.LogInformation("开始加载目录下所有规则集，目录路径: {DirectoryPath}", directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"目录不存在: {directoryPath}");
        }

        var allRuleSets = new List<IRuleSet>();

        // 遍历所有 Excel 文件
        var excelFiles = Directory.GetFiles(directoryPath, "*.xlsx", SearchOption.AllDirectories);
        
        _logger.LogDebug("找到 {Count} 个 Excel 文件", excelFiles.Length);

        foreach (var filePath in excelFiles)
        {
            try
            {
                var category = InferRuleCategory(filePath);
                
                if (_loaders.TryGetValue(category, out var loader))
                {
                    var ruleSet = loader.LoadRuleSetAsync(filePath, cancellationToken).GetAwaiter().GetResult();
                    allRuleSets.Add(ruleSet);
                    
                    _logger.LogInformation("成功加载规则集: {RuleCode}，类别: {Category}", ruleSet.RuleCode, category);
                }
                else
                {
                    _logger.LogWarning("跳过不支持的规则文件: {FilePath}，类别: {Category}", filePath, category);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "加载规则文件失败: {FilePath}", filePath);
            }
        }

        _logger.LogInformation("规则集加载完成，共加载 {Count} 个规则集", allRuleSets.Count);

        return Task.FromResult<IReadOnlyList<IRuleSet>>(allRuleSets);
    }

    /// <summary>
    /// 根据文件路径推断规则类别
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>规则类别</returns>
    private static RuleCategory InferRuleCategory(string filePath)
    {
        var fileName = Path.GetFileName(filePath).ToLowerInvariant();

        if (fileName.Contains("重复收费") || fileName.Contains("duplicate"))
        {
            return RuleCategory.DuplicateCharge;
        }

        if (fileName.Contains("限定频次") || fileName.Contains("frequency"))
        {
            return RuleCategory.FrequencyLimit;
        }

        // 默认返回重复收费类别
        return RuleCategory.DuplicateCharge;
    }
}
