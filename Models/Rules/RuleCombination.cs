namespace SettlementMcpServer.Models.Rules;

/// <summary>
/// 规则执行模式
/// </summary>
public enum ExecutionMode
{
    /// <summary>
    /// 顺序执行：按规则编码依次执行
    /// </summary>
    Sequential,

    /// <summary>
    /// 并行执行：所有规则同时执行
    /// </summary>
    Parallel
}

/// <summary>
/// 规则组合模型，将多条规则组合为一个整体进行批量执行
/// </summary>
public class RuleCombination
{
    /// <summary>组合编码</summary>
    public string CombinationCode { get; set; } = string.Empty;

    /// <summary>组合名称</summary>
    public string CombinationName { get; set; } = string.Empty;

    /// <summary>执行模式（顺序/并行）</summary>
    public ExecutionMode Mode { get; set; }

    /// <summary>组合中包含的规则编码列表</summary>
    public List<string> RuleCodes { get; set; } = [];
}
