namespace SettlementMcpServer.Models.Rules;

#region 枚举定义

/// <summary>
/// 时间间隔类型枚举
/// </summary>
public enum TimeIntervalType
{
    /// <summary>
    /// 一次就诊过程：直接判断结算单据的 Numbers 数量是否大于限定次数
    /// </summary>
    OneVisit = 1,

    /// <summary>
    /// 天：根据 TimeInterval 数值计算当前项目编码在时间窗口内出现的次数
    /// </summary>
    Day = 3,

    /// <summary>
    /// 自定义周期（按天数最小单位）：根据 TimeInterval 数值计算当前项目编码在时间窗口内出现的次数
    /// </summary>
    CustomDay = 4,

    /// <summary>
    /// 月：根据 TimeInterval 数值计算当前项目编码在时间窗口内出现的次数
    /// </summary>
    Month = 6
}

#endregion

#region 限定频次规则模型

/// <summary>
/// 限定频次规则主内涵表模型
/// </summary>
public class FrequencyLimitRule
{
    /// <summary>项目编码</summary>
    public string ItemCode { get; set; } = string.Empty;

    /// <summary>项目名称</summary>
    public string ItemName { get; set; } = string.Empty;

    /// <summary>时间间隔</summary>
    public int TimeInterval { get; set; }

    /// <summary>住院限定次数</summary>
    public decimal? InpatientLimitCount { get; set; }

    /// <summary>门诊限定次数</summary>
    public decimal? OutpatientLimitCount { get; set; }

    /// <summary>频次计算方式</summary>
    public string FrequencyCalcMethod { get; set; } = string.Empty;

    /// <summary>提示信息</summary>
    public string PromptMessage { get; set; } = string.Empty;

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
    public TimeIntervalType TimeIntervalType { get; set; }

    /// <summary>有效开始时间</summary>
    public DateTime? ValidStartDate { get; set; }

    /// <summary>有效结束时间</summary>
    public DateTime? ValidEndDate { get; set; }

    /// <summary>项目编码列表（从 ItemCode 按 '|' 分隔解析）</summary>
    public IReadOnlyList<string> ItemCodes { get; set; } = Array.Empty<string>();
}

#endregion

#region 限定频次规则集模型

/// <summary>
/// 限定频次规则集聚合根
/// </summary>
public class FrequencyLimitRuleSet : IRuleSet
{
    /// <summary>内涵规则列表（Excel 中的所有规则行）</summary>
    public IReadOnlyList<FrequencyLimitRule> Rules { get; set; } = Array.Empty<FrequencyLimitRule>();

    /// <inheritdoc />
    public string RuleName { get; init; } = string.Empty;

    /// <inheritdoc />
    public RuleCategory Category => RuleCategory.限定频次规则;
}

#endregion
