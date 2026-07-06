namespace SettlementMcpServer.Models.Rules;

#region 枚举定义

/// <summary>
/// 重复收费规则类型枚举（仅用于重复收费规则内部的逻辑子类型）
/// </summary>
public enum DuplicateChargeRuleType
{
    /// <summary>
    /// 跨组共存：同一人同一天同组内，任一A组和任一B组内项目同时收取，提示B组内项目违规
    /// </summary>
    CrossGroupCoexist = 1,

    /// <summary>
    /// 组内重价：同A组内重复，同一天对项目收费单价低（除最高价以外项目）进行提示
    /// </summary>
    SameGroupDuplicate = 2,

    /// <summary>
    /// 阈值后存在：先判断A组内项目总数量大于累计扣费阈值，再判断是否存在B组项目，若存在进行提示
    /// </summary>
    ThresholdThenExist = 3,

    /// <summary>
    /// 跨组数量阈值：同一人同一天同组内，任一A组和任一B组内项目同时收取，判断B组项目收费数量大于累计扣费阈值
    /// </summary>
    CrossGroupQuantityThreshold = 4
}

#endregion

#region 规则模型

/// <summary>
/// 重复收费规则主内涵表模型
/// </summary>
public class DuplicateChargeRule
{
    /// <summary>规则编码（负页清单编码）</summary>
    public string RuleCode { get; set; } = string.Empty;

    /// <summary>状态</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>项目编码组A</summary>
    public string GroupCodeA { get; set; } = string.Empty;

    /// <summary>重复项目编码组B</summary>
    public string GroupCodeB { get; set; } = string.Empty;

    /// <summary>重复类型逻辑（枚举）</summary>
    public DuplicateChargeRuleType RuleType { get; set; }

    /// <summary>累计扣费阈值</summary>
    public decimal? DeductionThreshold { get; set; }

    /// <summary>除外条件</summary>
    public string? ExclusionCondition { get; set; }

    /// <summary>提示信息</summary>
    public string PromptMessage { get; set; } = string.Empty;

    /// <summary>是否审核收费时间相同</summary>
    public bool CheckSameChargeTime { get; set; }

    /// <summary>备注信息</summary>
    public string? Remark { get; set; }

    /// <summary>是否审核同一个执行科室</summary>
    public bool CheckSameExecDept { get; set; }

    /// <summary>有效开始时间</summary>
    public DateTime? ValidStartDate { get; set; }

    /// <summary>有效结束时间</summary>
    public DateTime? ValidEndDate { get; set; }
}

#endregion

#region 分组项目模型

/// <summary>
/// 分组项目A表模型
/// </summary>
public class RuleGroupAItem
{
    /// <summary>项目编码组A</summary>
    public string GroupCodeA { get; set; } = string.Empty;

    /// <summary>名称</summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>项目编码A</summary>
    public string ItemCodeA { get; set; } = string.Empty;

    /// <summary>最小数量</summary>
    public int? MinQuantity { get; set; }
}

/// <summary>
/// 分组项目B表模型
/// </summary>
public class RuleGroupBItem
{
    /// <summary>重复项目编码组B</summary>
    public string GroupCodeB { get; set; } = string.Empty;

    /// <summary>名称</summary>
    public string GroupName { get; set; } = string.Empty;

    /// <summary>项目编码B</summary>
    public string ItemCodeB { get; set; } = string.Empty;
}

#endregion

#region 规则集模型

/// <summary>
/// 重复收费规则集聚合根，将主内涵规则与A/B组项目关联为一个完整规则集
/// </summary>
public class DuplicateChargeRuleSet : IRuleSet
{
    /// <summary>主内涵规则</summary>
    public DuplicateChargeRule Rule { get; set; } = null!;

    /// <inheritdoc />
    public string RuleCode => Rule.RuleCode;

    /// <inheritdoc />
    public RuleCategory Category => RuleCategory.DuplicateCharge;

    /// <summary>A组项目列表</summary>
    public IReadOnlyList<RuleGroupAItem> GroupAItems { get; set; } = Array.Empty<RuleGroupAItem>();

    /// <summary>B组项目列表</summary>
    public IReadOnlyList<RuleGroupBItem> GroupBItems { get; set; } = Array.Empty<RuleGroupBItem>();
}

#endregion
