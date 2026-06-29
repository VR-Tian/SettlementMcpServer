using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;

namespace SettlementMcpServer.Tools;

/// <summary>
/// 粤海医保结算服务工具
/// </summary>
internal class YuehaiSettlementTools
{
    private readonly IYuehaiSettlementDataRepository _repository;
    private readonly ILogger<YuehaiSettlementTools> _logger;

    public YuehaiSettlementTools(
        IYuehaiSettlementDataRepository repository,
        ILogger<YuehaiSettlementTools>? logger = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _logger = logger ?? NullLogger<YuehaiSettlementTools>.Instance;
    }

    /// <summary>
    /// 查询粤海医保结算数据
    /// </summary>
    [McpServerTool]
    [Description("查询粤海医保结算数据，返回符合查询条件的全部结果")]
    public async Task<IReadOnlyList<YuehaiSettlement>> QuerySettlementsAsync(
        [Description("就诊ID（可选）")] string? visitId = null,
        [Description("结算ID（可选）")] string? settlementId = null,
        [Description("人员编号（可选）")] string? personnelNo = null,
        [Description("病历号（可选）")] string? medicalRecordNo = null,
        [Description("住院/门诊号（可选）")] string? inpatientOutpatientNo = null,
        [Description("险种类型（可选）")] string? insuranceType = null,
        [Description("医疗类别（可选）")] string? medicalCategory = null,
        CancellationToken cancellationToken = default)
    {
        var filter = new YuehaiSettlementQueryFilter
        {
            VisitId = visitId,
            SettlementId = settlementId,
            PersonnelNo = personnelNo,
            MedicalRecordNo = medicalRecordNo,
            InpatientOutpatientNo = inpatientOutpatientNo,
            InsuranceType = insuranceType,
            MedicalCategory = medicalCategory,
        };

        return await _repository.QueryAllSettlementsAsync(filter, cancellationToken);
    }
}
