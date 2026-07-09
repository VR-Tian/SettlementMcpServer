using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiniExcelLibs;
using MiniExcelLibs.Attributes;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models.Rules;

namespace SettlementMcpServer.Infrastructure.Rules;

/// <summary>
/// 重复收费规则 Excel 加载器实现
/// </summary>
/// <remarks>
/// <para>
/// 使用 <b>MiniExcel</b> 作为 Excel 读取组件，从 Excel 规则内涵文件中加载三表数据：
/// </para>
/// <list type="number">
///   <item><description>第一个 Sheet 为内涵规则表（<see cref="DuplicateChargeRule"/>），每行一条规则</description></item>
///   <item><description>第二个 Sheet 为分组项目A表（<see cref="RuleGroupAItem"/>）</description></item>
///   <item><description>第三个 Sheet 为分组项目B表（<see cref="RuleGroupBItem"/>）</description></item>
/// </list>
/// <para>
/// 通过 <c>GroupCodeA</c> 和 <c>GroupCodeB</c> 关联三表数据，组装为 <see cref="DuplicateChargeRuleSet"/> 规则集聚合根。
/// </para>
/// </remarks>
public sealed class DuplicateChargeExcelRuleLoader : IRuleLoader
{
    private readonly ILogger<DuplicateChargeExcelRuleLoader> _logger;

    /// <summary>
    /// 初始化重复收费规则 Excel 加载器实例
    /// </summary>
    /// <param name="logger">日志记录器（由 DI 注入，可选）</param>
    public DuplicateChargeExcelRuleLoader(ILogger<DuplicateChargeExcelRuleLoader>? logger = null)
    {
        _logger = logger ?? NullLogger<DuplicateChargeExcelRuleLoader>.Instance;
    }

    /// <inheritdoc />
    public RuleCategory SupportedCategory => RuleCategory.重复收费规则;

    /// <inheritdoc />
    public async Task<IRuleSet> LoadRuleSetAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException($"规则文件不存在: {filePath}", filePath);
        }

        _logger.LogInformation("开始从 Excel 文件加载重复收费规则数据，文件路径: {FilePath}", filePath);

        // 从文件名提取规则名称（不含扩展名）
        var ruleName = Path.GetFileNameWithoutExtension(filePath);

        // 获取 Excel 文件中的所有 Sheet 名称（MiniExcel 的 GetSheetNames 是同步方法）
        var sheetNames = MiniExcel.GetSheetNames(filePath).ToList();

        if (sheetNames.Count < 3)
        {
            throw new InvalidOperationException(
                $"Excel 文件必须包含至少 3 个工作表，当前文件包含 {sheetNames.Count} 个工作表");
        }

        // 读取三个 Sheet 的数据
        var rules = await ReadSheetAsync<DuplicateChargeRuleExcelRow>(filePath, sheetNames[0], cancellationToken);
        var groupAItems = await ReadSheetAsync<RuleGroupAItemExcelRow>(filePath, sheetNames[1], cancellationToken);
        var groupBItems = await ReadSheetAsync<RuleGroupBItemExcelRow>(filePath, sheetNames[2], cancellationToken);

        _logger.LogInformation(
            "Excel 重复收费规则数据加载完成，内涵规则: {RuleCount} 条，分组项目A表: {GroupACount} 条，分组项目B表: {GroupBCount} 条",
            rules.Count, groupAItems.Count, groupBItems.Count);

        // 将 Excel 行模型映射为领域模型
        var domainRules = rules.Select(MapToDomainRule).ToList();
        var domainGroupAItems = groupAItems.Select(MapToDomainGroupAItem).ToList();
        var domainGroupBItems = groupBItems.Select(MapToDomainGroupBItem).ToList();

        // 构建规则集（内涵由多条规则组成）
        if (domainRules.Count == 0)
        {
            throw new InvalidOperationException("Excel 文件中未找到有效的重复收费规则数据");
        }

        // 为每条规则关联对应的 A/B 组项目
        foreach (var rule in domainRules)
        {
            rule.GroupAItems = domainGroupAItems
                .Where(g => g.GroupCodeA == rule.GroupCodeA)
                .ToList()
                .AsReadOnly();

            rule.GroupBItems = domainGroupBItems
                .Where(g => g.GroupCodeB == rule.GroupCodeB)
                .ToList()
                .AsReadOnly();
        }

        var ruleSet = new DuplicateChargeRuleSet
        {
            RuleName = ruleName,
            Rules = domainRules.AsReadOnly()
        };

        _logger.LogInformation("重复收费规则集构建完成，规则名称: {RuleName}", ruleName);

        return ruleSet;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IRuleSet>> LoadAllRuleSetsAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"重复收费规则文件目录不存在: {directoryPath}");
        }

        _logger.LogInformation("开始从目录加载所有重复收费规则集，目录路径: {DirectoryPath}", directoryPath);

        var excelFiles = Directory.GetFiles(directoryPath, "*.xlsx", SearchOption.AllDirectories);

        if (excelFiles.Length == 0)
        {
            throw new InvalidOperationException($"重复收费规则文件目录中未找到 Excel 文件: {directoryPath}");
        }

        var allRuleSets = new List<IRuleSet>();

        foreach (var excelFile in excelFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var ruleSet = await LoadRuleSetAsync(excelFile, cancellationToken);
                allRuleSets.Add(ruleSet);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "加载重复收费规则文件失败: {FilePath}，跳过", excelFile);
            }
        }

        if (allRuleSets.Count == 0)
        {
            throw new InvalidOperationException("目录中未找到有效的重复收费规则数据");
        }

        _logger.LogInformation("所有重复收费规则集构建完成，共 {RuleSetCount} 个规则集", allRuleSets.Count);

        return allRuleSets.AsReadOnly();
    }

    /// <summary>
    /// 从指定的 Sheet 读取数据
    /// </summary>
    /// <typeparam name="T">Excel 行模型类型</typeparam>
    /// <param name="filePath">Excel 文件路径</param>
    /// <param name="sheetName">工作表名称</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>读取的数据行列表</returns>
    private static async Task<List<T>> ReadSheetAsync<T>(
        string filePath,
        string sheetName,
        CancellationToken cancellationToken)
        where T : class, new()
    {
        var result = await MiniExcel.QueryAsync<T>(
            filePath,
            sheetName: sheetName,
            cancellationToken: cancellationToken);

        return result.ToList();
    }

    /// <summary>
    /// 将 Excel 行模型映射为领域模型 DuplicateChargeRule
    /// </summary>
    /// <param name="row">Excel 行模型</param>
    /// <returns>领域模型</returns>
    private static DuplicateChargeRule MapToDomainRule(DuplicateChargeRuleExcelRow row)
    {
        return new DuplicateChargeRule
        {
            Status = row.Status ?? string.Empty,
            GroupCodeA = row.GroupCodeA ?? string.Empty,
            GroupCodeB = row.GroupCodeB ?? string.Empty,
            RuleType = ParseRuleType(row.RuleType),
            DeductionThreshold = row.DeductionThreshold,
            ExclusionCondition = row.ExclusionCondition,
            PromptMessage = row.PromptMessage ?? string.Empty,
            CheckSameChargeTime = row.CheckSameChargeTime,
            Remark = row.Remark,
            CheckSameExecDept = row.CheckSameExecDept,
            ValidStartDate = row.ValidStartDate,
            ValidEndDate = row.ValidEndDate
        };
    }

    /// <summary>
    /// 将 Excel 行模型映射为领域模型 RuleGroupAItem
    /// </summary>
    /// <param name="row">Excel 行模型</param>
    /// <returns>领域模型</returns>
    private static RuleGroupAItem MapToDomainGroupAItem(RuleGroupAItemExcelRow row)
    {
        return new RuleGroupAItem
        {
            GroupCodeA = row.GroupCodeA ?? string.Empty,
            GroupName = row.GroupName ?? string.Empty,
            ItemCodeA = row.ItemCodeA ?? string.Empty,
            MinQuantity = row.MinQuantity
        };
    }

    /// <summary>
    /// 将 Excel 行模型映射为领域模型 RuleGroupBItem
    /// </summary>
    /// <param name="row">Excel 行模型</param>
    /// <returns>领域模型</returns>
    private static RuleGroupBItem MapToDomainGroupBItem(RuleGroupBItemExcelRow row)
    {
        return new RuleGroupBItem
        {
            GroupCodeB = row.GroupCodeB ?? string.Empty,
            GroupName = row.GroupName ?? string.Empty,
            ItemCodeB = row.ItemCodeB ?? string.Empty
        };
    }

    /// <summary>
    /// 解析规则类型枚举
    /// </summary>
    /// <param name="ruleTypeValue">规则类型值（可能是整数或字符串）</param>
    /// <returns>规则类型枚举</returns>
    private static DuplicateChargeRuleType ParseRuleType(object? ruleTypeValue)
    {
        if (ruleTypeValue == null)
        {
            return DuplicateChargeRuleType.CrossGroupCoexist;
        }

        if (ruleTypeValue is int intValue)
        {
            return (DuplicateChargeRuleType)intValue;
        }

        if (ruleTypeValue is long longValue)
        {
            return (DuplicateChargeRuleType)(int)longValue;
        }

        if (ruleTypeValue is double doubleValue)
        {
            return (DuplicateChargeRuleType)(int)doubleValue;
        }

        if (ruleTypeValue is string strValue && int.TryParse(strValue, out var parsed))
        {
            return (DuplicateChargeRuleType)parsed;
        }

        return DuplicateChargeRuleType.CrossGroupCoexist;
    }

    /// <summary>
    /// 重复收费规则 Excel 行模型
    /// </summary>
    private sealed class DuplicateChargeRuleExcelRow
    {
        [ExcelColumnName("状态")]
        public string? Status { get; set; }

        [ExcelColumnName("项目编码组A")]
        public string? GroupCodeA { get; set; }

        [ExcelColumnName("重复项目编码组B")]
        public string? GroupCodeB { get; set; }

        [ExcelColumnName("重复类型逻辑")]
        public object? RuleType { get; set; }

        [ExcelColumnName("累计扣费阈值")]
        public decimal? DeductionThreshold { get; set; }

        [ExcelColumnName("除外条件")]
        public string? ExclusionCondition { get; set; }

        [ExcelColumnName("提示信息")]
        public string? PromptMessage { get; set; }

        [ExcelColumnName("是否审核收费时间相同")]
        public bool? CheckSameChargeTime { get; set; }

        [ExcelColumnName("备注信息")]
        public string? Remark { get; set; }

        [ExcelColumnName("是否审核同一个执行科室")]
        public bool? CheckSameExecDept { get; set; }

        [ExcelColumnName("有效开始时间")]
        public DateTime? ValidStartDate { get; set; }

        [ExcelColumnName("有效结束时间")]
        public DateTime? ValidEndDate { get; set; }
    }

    /// <summary>
    /// 分组项目A表 Excel 行模型
    /// </summary>
    private sealed class RuleGroupAItemExcelRow
    {
        [ExcelColumnName("项目编码组A")]
        public string? GroupCodeA { get; set; }

        [ExcelColumnName("名称")]
        public string? GroupName { get; set; }

        [ExcelColumnName("项目编码A")]
        public string? ItemCodeA { get; set; }

        [ExcelColumnName("最小数量")]
        public int? MinQuantity { get; set; }
    }

    /// <summary>
    /// 分组项目B表 Excel 行模型
    /// </summary>
    private sealed class RuleGroupBItemExcelRow
    {
        [ExcelColumnName("重复项目编码组B")]
        public string? GroupCodeB { get; set; }

        [ExcelColumnName("名称")]
        public string? GroupName { get; set; }

        [ExcelColumnName("项目编码B")]
        public string? ItemCodeB { get; set; }
    }
}
