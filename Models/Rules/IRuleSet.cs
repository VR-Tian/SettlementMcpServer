namespace SettlementMcpServer.Models.Rules;

/// <summary>
/// 通用规则集接口
/// </summary>
/// <remarks>
/// <para>
/// 所有规则集类型都实现此接口，作为规则引擎的统一抽象。
/// </para>
/// </remarks>
public interface IRuleSet
{
    /// <summary>规则名称（来源于文件名）</summary>
    string RuleName { get; }

    /// <summary>规则类别</summary>
    RuleCategory Category { get; }
}
