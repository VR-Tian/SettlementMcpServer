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
    /// <summary>规则编码</summary>
    public string RuleCode { get; set; } = string.Empty;

    /// <summary>规则类别</summary>
    public RuleCategory RuleCategory { get; set; }

    /// <summary>人员编号</summary>
    public string PersonnelNo { get; set; } = string.Empty;

    /// <summary>费用发生日期</summary>
    public string FeeOccurrenceDate { get; set; } = string.Empty;

    /// <summary>定点医药机构编号</summary>
    public string InstitutionCode { get; set; } = string.Empty;

    /// <summary>定点医药机构名称</summary>
    public string InstitutionName { get; set; } = string.Empty;

    /// <summary>项目编码组A</summary>
    public string GroupCodeA { get; set; } = string.Empty;

    /// <summary>重复项目编码组B</summary>
    public string GroupCodeB { get; set; } = string.Empty;

    /// <summary>违规项目编码</summary>
    public string ViolationItemCode { get; set; } = string.Empty;

    /// <summary>违规项目名称</summary>
    public string ViolationItemName { get; set; } = string.Empty;

    /// <summary>违规数量</summary>
    public decimal? ViolationQuantity { get; set; }

    /// <summary>违规单价</summary>
    public decimal? ViolationUnitPrice { get; set; }

    /// <summary>违规金额</summary>
    public decimal? ViolationAmount { get; set; }

    /// <summary>提示信息</summary>
    public string PromptMessage { get; set; } = string.Empty;

    /// <summary>受单科室编码</summary>
    public string? ReceivingDeptCode { get; set; }

    /// <summary>受单科室名称</summary>
    public string? ReceivingDeptName { get; set; }
}
