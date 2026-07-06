namespace SettlementMcpServer.Models.Rules;

/// <summary>
/// 规则类别枚举
/// </summary>
public enum RuleCategory
{
    /// <summary>重复收费规则</summary>
    DuplicateCharge = 1,

    /// <summary>限定频次规则</summary>
    FrequencyLimit = 2
}
