using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using MiniExcelLibs;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models.Rules;

namespace SettlementMcpServer.Infrastructure.Rules;

/// <summary>
/// 限定频次规则 Excel 加载器
/// </summary>
/// <remarks>
/// <para>
/// 负责从 Excel 文件中加载限定频次规则。
/// Excel 文件包含一个工作表：主内涵表（<see cref="FrequencyLimitRule"/>）。
/// </para>
/// </remarks>
public sealed class FrequencyLimitExcelRuleLoader : IRuleLoader
{
    private readonly ILogger<FrequencyLimitExcelRuleLoader> _logger;

    /// <summary>
    /// 初始化限定频次规则 Excel 加载器
    /// </summary>
    /// <param name="logger">日志记录器</param>
    public FrequencyLimitExcelRuleLoader(ILogger<FrequencyLimitExcelRuleLoader>? logger = null)
    {
        _logger = logger ?? NullLogger<FrequencyLimitExcelRuleLoader>.Instance;
    }

    /// <inheritdoc />
    public RuleCategory SupportedCategory => RuleCategory.限定频次规则;

    /// <inheritdoc />
    public Task<IRuleSet> LoadRuleSetAsync(string filePath, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);

        _logger.LogInformation("开始从 Excel 文件加载限定频次规则，文件路径: {FilePath}", filePath);

        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("限定频次规则 Excel 文件不存在", filePath);
        }

        // 从文件名提取规则名称（不含扩展名）
        var ruleName = Path.GetFileNameWithoutExtension(filePath);

        // 读取主内涵表（所有规则行）
        var rules = LoadFrequencyLimitRules(filePath);

        if (rules.Count == 0)
        {
            throw new InvalidOperationException("Excel 文件中未找到限定频次规则数据");
        }

        var ruleSet = new FrequencyLimitRuleSet
        {
            RuleName = ruleName,
            Rules = rules
        };

        _logger.LogInformation(
            "限定频次规则加载完成，规则名称: {RuleName}，规则数量: {RuleCount}",
            ruleName,
            rules.Count);

        return Task.FromResult<IRuleSet>(ruleSet);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<IRuleSet>> LoadAllRuleSetsAsync(
        string directoryPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(directoryPath);

        if (!Directory.Exists(directoryPath))
        {
            throw new DirectoryNotFoundException($"限定频次规则文件目录不存在: {directoryPath}");
        }

        _logger.LogInformation("开始从目录加载所有限定频次规则集，目录路径: {DirectoryPath}", directoryPath);

        var excelFiles = Directory.GetFiles(directoryPath, "*.xlsx", SearchOption.AllDirectories);

        if (excelFiles.Length == 0)
        {
            throw new InvalidOperationException($"限定频次规则文件目录中未找到 Excel 文件: {directoryPath}");
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
                _logger.LogWarning(ex, "加载限定频次规则文件失败: {FilePath}，跳过", excelFile);
            }
        }

        if (allRuleSets.Count == 0)
        {
            throw new InvalidOperationException("目录中未找到有效的限定频次规则数据");
        }

        _logger.LogInformation("所有限定频次规则集构建完成，共 {RuleSetCount} 个规则集", allRuleSets.Count);

        return allRuleSets.AsReadOnly();
    }

    /// <summary>
    /// 从 Excel 文件加载限定频次规则列表
    /// </summary>
    /// <param name="filePath">Excel 文件路径</param>
    /// <returns>规则列表</returns>
    private List<FrequencyLimitRule> LoadFrequencyLimitRules(string filePath)
    {
        using var stream = File.OpenRead(filePath);

        var rows = stream.Query<FrequencyLimitRuleExcelRow>(sheetName: "主内涵").ToList();

        _logger.LogDebug("从 Excel 读取到 {Count} 行数据", rows.Count);

        return rows.Select(MapToFrequencyLimitRule).ToList();
    }

    /// <summary>
    /// 将 Excel 行映射为限定频次规则
    /// </summary>
    /// <param name="row">Excel 行</param>
    /// <returns>限定频次规则</returns>
    private static FrequencyLimitRule MapToFrequencyLimitRule(FrequencyLimitRuleExcelRow row)
    {
        var itemCode = row.ItemCode?.Trim() ?? string.Empty;
        return new FrequencyLimitRule
        {
            ItemCode = itemCode,
            ItemName = row.ItemName?.Trim() ?? string.Empty,
            TimeInterval = ConvertToInt(row.TimeInterval),
            InpatientLimitCount = row.InpatientLimitCount,
            OutpatientLimitCount = row.OutpatientLimitCount,
            FrequencyCalcMethod = row.FrequencyCalcMethod?.Trim() ?? string.Empty,
            PromptMessage = row.PromptMessage?.Trim() ?? string.Empty,
            InpatientDaysIncludeBoth = row.InpatientDaysIncludeBoth,
            CheckInsufficientCount = row.CheckInsufficientCount,
            IsTotalViolation = row.IsTotalViolation,
            CheckOutpatientAndDept = row.CheckOutpatientAndDept,
            CheckInpatientAndDept = row.CheckInpatientAndDept,
            LimitAmount = row.LimitAmount,
            CheckSameExecDept = row.CheckSameExecDept,
            TimeIntervalType = ParseTimeIntervalType(row.TimeIntervalType),
            ValidStartDate = row.ValidStartDate,
            ValidEndDate = row.ValidEndDate,
            ItemCodes = ParseItemCodes(itemCode)
        };
    }

    /// <summary>
    /// 解析项目编码列表（以分隔符'|'组合）
    /// </summary>
    /// <param name="itemCodeStr">项目编码字符串</param>
    /// <returns>项目编码列表</returns>
    private static IReadOnlyList<string> ParseItemCodes(string itemCodeStr)
    {
        if (string.IsNullOrWhiteSpace(itemCodeStr))
        {
            return Array.Empty<string>();
        }

        return itemCodeStr
            .Split('|', StringSplitOptions.RemoveEmptyEntries)
            .Select(code => code.Trim())
            .Where(code => !string.IsNullOrEmpty(code))
            .ToList()
            .AsReadOnly();
    }

    #region 类型转换辅助方法

    /// <summary>
    /// 解析时间间隔类型枚举
    /// </summary>
    /// <param name="value">时间间隔类型值</param>
    /// <returns>时间间隔类型枚举</returns>
    private static TimeIntervalType ParseTimeIntervalType(object? value)
    {
        if (value == null)
        {
            return TimeIntervalType.OneVisit;
        }

        if (value is int intValue)
        {
            return (TimeIntervalType)intValue;
        }

        if (value is long longValue)
        {
            return (TimeIntervalType)(int)longValue;
        }

        if (value is double doubleValue)
        {
            return (TimeIntervalType)(int)doubleValue;
        }

        if (value is string strValue && int.TryParse(strValue, out var parsed))
        {
            return (TimeIntervalType)parsed;
        }

        return TimeIntervalType.OneVisit;
    }

    private static int ConvertToInt(object? value)
    {
        return value switch
        {
            null => 0,
            int i => i,
            long l => (int)l,
            double d => (int)d,
            decimal m => (int)m,
            string s when int.TryParse(s, out var result) => result,
            _ => 0
        };
    }

    private static int? ConvertToNullableInt(object? value)
    {
        return value switch
        {
            null => null,
            int i => i,
            long l => (int)l,
            double d => (int)d,
            decimal m => (int)m,
            string s when int.TryParse(s, out var result) => result,
            _ => null
        };
    }

    private static decimal? ConvertToNullableDecimal(object? value)
    {
        return value switch
        {
            null => null,
            int i => i,
            long l => l,
            double d => (decimal)d,
            decimal m => m,
            string s when decimal.TryParse(s, out var result) => result,
            _ => null
        };
    }

    private static bool ConvertToBool(object? value)
    {
        return value switch
        {
            null => false,
            bool b => b,
            int i => i != 0,
            long l => l != 0,
            double d => Math.Abs(d) > 0.001,
            decimal m => Math.Abs(m) > 0.001m,
            string s when bool.TryParse(s, out var result) => result,
            string s when int.TryParse(s, out var intResult) => intResult != 0,
            string s => s.Equals("是", StringComparison.OrdinalIgnoreCase) ||
                        s.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                        s.Equals("yes", StringComparison.OrdinalIgnoreCase) ||
                        s.Equals("1", StringComparison.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static DateTime? ConvertToNullableDateTime(object? value)
    {
        return value switch
        {
            null => null,
            DateTime dt => dt,
            string s when DateTime.TryParse(s, out var result) => result,
            _ => null
        };
    }

    #endregion

    /// <summary>
    /// 限定频次规则 Excel 行模型
    /// </summary>
    private sealed class FrequencyLimitRuleExcelRow
    {
        /// <summary>项目编码</summary>
        public string ItemCode { get; set; }

        /// <summary>项目名称</summary>
        public string ItemName { get; set; }

        /// <summary>时间间隔</summary>
        public string TimeInterval { get; set; }

        /// <summary>住院限定次数</summary>
        public int? InpatientLimitCount { get; set; }

        /// <summary>门诊限定次数</summary>
        public int? OutpatientLimitCount { get; set; }

        /// <summary>频次计算方式</summary>
        public string FrequencyCalcMethod { get; set; }

        /// <summary>提示信息</summary>
        public string PromptMessage { get; set; }

        /// <summary>住院天数算头算尾</summary>
        public bool? InpatientDaysIncludeBoth { get; set; }

        /// <summary>是否审核次数不足</summary>
        public bool? CheckInsufficientCount { get; set; }

        /// <summary>是否总数违规</summary>
        public bool? IsTotalViolation { get; set; }

        /// <summary>是否审核门诊及限定科室</summary>
        public bool? CheckOutpatientAndDept { get; set; }

        /// <summary>是否审核住院及限定科室</summary>
        public bool? CheckInpatientAndDept { get; set; }

        /// <summary>限定总金额</summary>
        public decimal? LimitAmount { get; set; }

        /// <summary>是否审核同一个执行科室</summary>
        public bool? CheckSameExecDept { get; set; }

        /// <summary>时间间隔类型</summary>
        public string TimeIntervalType { get; set; }

        /// <summary>有效开始时间</summary>
        public DateTime? ValidStartDate { get; set; }

        /// <summary>有效结束时间</summary>
        public DateTime? ValidEndDate { get; set; }
    }
}
