using System.Text.Json.Serialization;
using SettlementMcpServer.Models;

namespace SettlementMcpServer.Models.Rules;

/// <summary>
/// 规则违规结果模型，记录单条违规明细
/// </summary>
/// <remarks>
/// <para>
/// 该模型为通用规则违规结果，适用于所有规则类别（重复收费、限定频次等）。
/// </para>
/// </remarks>
public class RuleViolation
{
    /// <summary>规则名称（来源于文件名）</summary>
    [JsonPropertyName("规则名称")]
    public string RuleName { get; set; } = string.Empty;

    /// <summary>规则类别</summary>
    [JsonPropertyName("规则类别")]
    public RuleCategory RuleCategory { get; set; }

    /// <summary>人员编号</summary>
    [JsonPropertyName("人员编号")]
    public string PersonnelNo { get; set; } = string.Empty;

    /// <summary>费用发生日期</summary>
    [JsonPropertyName("费用发生日期")]
    public string FeeOccurrenceDate { get; set; } = string.Empty;

    /// <summary>定点医药机构编号</summary>
    [JsonPropertyName("定点医药机构编号")]
    public string InstitutionCode { get; set; } = string.Empty;

    /// <summary>定点医药机构名称</summary>
    [JsonPropertyName("定点医药机构名称")]
    public string InstitutionName { get; set; } = string.Empty;

    /// <summary>项目编码组A</summary>
    [JsonPropertyName("项目编码组A")]
    public string GroupCodeA { get; set; } = string.Empty;

    /// <summary>重复项目编码组B</summary>
    [JsonPropertyName("重复项目编码组B")]
    public string GroupCodeB { get; set; } = string.Empty;

    /// <summary>违规项目编码</summary>
    [JsonPropertyName("违规项目编码")]
    public string ViolationItemCode { get; set; } = string.Empty;

    /// <summary>违规项目名称</summary>
    [JsonPropertyName("违规项目名称")]
    public string ViolationItemName { get; set; } = string.Empty;

    /// <summary>违规数量</summary>
    [JsonPropertyName("违规数量")]
    public decimal? ViolationQuantity { get; set; }

    /// <summary>违规单价</summary>
    [JsonPropertyName("违规单价")]
    public decimal? ViolationUnitPrice { get; set; }

    /// <summary>违规金额</summary>
    [JsonPropertyName("违规金额")]
    public decimal? ViolationAmount { get; set; }

    /// <summary>提示信息</summary>
    [JsonPropertyName("提示信息")]
    public string PromptMessage { get; set; } = string.Empty;

    /// <summary>受单科室编码</summary>
    [JsonPropertyName("受单科室编码")]
    public string? ReceivingDeptCode { get; set; }

    /// <summary>受单科室名称</summary>
    [JsonPropertyName("受单科室名称")]
    public string? ReceivingDeptName { get; set; }

    /// <summary>
    /// 关联项目列表（根据规则内涵逻辑组成的医保结算费用项目）
    /// </summary>
    /// <remarks>
    /// <para>
    /// 记录当前违规所涉及的关联医保结算费用项目。例如：
    /// </para>
    /// <list type="bullet">
    ///   <item><description>重复收费-跨组共存：A组和B组中所有匹配的费用项目</description></item>
    ///   <item><description>重复收费-组内重复低价：同组内所有匹配的费用项目</description></item>
    ///   <item><description>限定频次：同一时间窗口内所有超限的费用项目</description></item>
    /// </list>
    /// </remarks>
    [JsonPropertyName("关联项目")]
    public List<Settlement> RelatedSettlements { get; set; } = [];
}
