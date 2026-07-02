using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;

namespace SettlementMcpServer.Tools;

/// <summary>
/// YueHai医保结算服务工具
/// </summary>
/// <remarks>
/// <para>
/// 此类封装了YueHai医保结算相关的 MCP 工具方法，客户端可通过 MCP 协议调用这些工具。
/// </para>
/// <para>
/// <b>完整查询导出流程（解决 MCP 上下文长度限制）：</b>
/// </para>
/// <list type="number">
///   <item>
///     <b>步骤 1：获取总数</b> - 调用 <see cref="GetSettlementCountAsync"/>
///     获取符合查询条件的总记录数和分页元数据。
///   </item>
///   <item>
///     <b>步骤 2：循环请求</b> - 根据返回的 <c>TotalPages</c>，循环调用
///     <see cref="QuerySettlementsAsync"/> 每次传入不同的 <c>page</c> 参数获取数据。
///   </item>
///   <item>
///     <b>步骤 3：导出 Excel</b> - 调用 <see cref="ExportSettlementsToExcelAsync"/>，
///     内部自动获取全部数据后导出为 Excel 文件。
///   </item>
/// </list>
/// </remarks>
internal class YuehaiSettlementTools
{
    private readonly IYuehaiSettlementDataRepository _repository;
    private readonly IExcelExportService _excelExportService;
    private readonly ILogger<YuehaiSettlementTools> _logger;

    public YuehaiSettlementTools(
        IYuehaiSettlementDataRepository repository,
        IExcelExportService excelExportService,
        ILogger<YuehaiSettlementTools>? logger = null)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _excelExportService = excelExportService ?? throw new ArgumentNullException(nameof(excelExportService));
        _logger = logger ?? NullLogger<YuehaiSettlementTools>.Instance;
    }

    /// <summary>
    /// 获取符合查询条件的总记录数和分页元数据
    /// </summary>
    /// <param name="visitId">就诊ID（可选，精确匹配）</param>
    /// <param name="settlementId">结算ID（可选，精确匹配）</param>
    /// <param name="personnelNo">人员编号（可选，精确匹配）</param>
    /// <param name="medicalRecordNo">病历号（可选，精确匹配）</param>
    /// <param name="inpatientOutpatientNo">住院/门诊号（可选，精确匹配）</param>
    /// <param name="insuranceType">险种类型（可选，精确匹配）</param>
    /// <param name="medicalCategory">医疗类别（可选，精确匹配）</param>
    /// <param name="pageSize">每页条数，默认 100，最大 500</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页元数据（总记录数、总页数、当前页等）</returns>
    /// <remarks>
    /// <para>
    /// <b>这是分页查询的第一步。</b>
    /// 客户端先调用此方法获取总记录数，然后根据返回的 <c>TotalPages</c>
    /// 循环调用 <see cref="QuerySettlementsAsync"/> 获取每页数据。
    /// </para>
    /// <para>
    /// <b>使用示例：</b>
    /// <code>
    /// // 步骤 1：获取总数
    /// var pagination = await GetSettlementCountAsync(personnelNo: "P001", pageSize: 100);
    /// // pagination.TotalCount = 500, pagination.TotalPages = 5
    ///
    /// // 步骤 2：循环请求每一页
    /// for (int page = 1; page &lt;= pagination.TotalPages; page++)
    /// {
    ///     var results = await QuerySettlementsAsync(personnelNo: "P001", page: page, pageSize: 100);
    ///     // 处理 results...
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    [McpServerTool]
    [Description("获取YueHai结算数据查询的总记录数和分页元数据，用于计算需要请求的页数")]
    public async Task<PaginationMetadata> GetSettlementCountAsync(
        [Description("就诊ID（可选）")] string? visitId = null,
        [Description("结算ID（可选）")] string? settlementId = null,
        [Description("人员编号（可选）")] string? personnelNo = null,
        [Description("病历号（可选）")] string? medicalRecordNo = null,
        [Description("住院/门诊号（可选）")] string? inpatientOutpatientNo = null,
        [Description("险种类型（可选）")] string? insuranceType = null,
        [Description("医疗类别（可选）")] string? medicalCategory = null,
        [Description("每页条数（默认100，最大500）")] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(visitId, settlementId, personnelNo, medicalRecordNo, inpatientOutpatientNo, insuranceType, medicalCategory);

        var totalCount = await _repository.CountSettlementsAsync(filter, cancellationToken);

        return new PaginationMetadata
        {
            TotalCount = totalCount,
            PageSize = pageSize,
            CurrentPage = 1,
        };
    }

    /// <summary>
    /// 分页查询YueHai医保结算数据
    /// </summary>
    /// <param name="visitId">就诊ID（可选，精确匹配）</param>
    /// <param name="settlementId">结算ID（可选，精确匹配）</param>
    /// <param name="personnelNo">人员编号（可选，精确匹配）</param>
    /// <param name="medicalRecordNo">病历号（可选，精确匹配）</param>
    /// <param name="inpatientOutpatientNo">住院/门诊号（可选，精确匹配）</param>
    /// <param name="insuranceType">险种类型（可选，精确匹配）</param>
    /// <param name="medicalCategory">医疗类别（可选，精确匹配）</param>
    /// <param name="page">页码，从 1 开始，默认 1</param>
    /// <param name="pageSize">每页条数，默认 100，最大 500</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前页的结算数据列表</returns>
    /// <remarks>
    /// <para>
    /// <b>这是分页查询的第二步。</b>
    /// 调用此方法前应先通过 <see cref="GetSettlementCountAsync"/> 获取总页数。
    /// </para>
    /// <para>
    /// <b>分页说明：</b>
    /// 每次请求返回的数据量受 <c>pageSize</c> 控制，确保不超出 MCP 上下文长度限制。
    /// 建议根据实际上下文容量调整 <c>pageSize</c>（默认 100 条）。
    /// </para>
    /// </remarks>
    [McpServerTool]
    [Description("分页查询YueHai结算数据，需配合 GetSettlementCountAsync 使用")]
    public async Task<IReadOnlyList<YuehaiSettlement>> QuerySettlementsAsync(
        [Description("就诊ID（可选）")] string? visitId = null,
        [Description("结算ID（可选）")] string? settlementId = null,
        [Description("人员编号（可选）")] string? personnelNo = null,
        [Description("病历号（可选）")] string? medicalRecordNo = null,
        [Description("住院/门诊号（可选）")] string? inpatientOutpatientNo = null,
        [Description("险种类型（可选）")] string? insuranceType = null,
        [Description("医疗类别（可选）")] string? medicalCategory = null,
        [Description("页码，从1开始（默认1）")] int page = 1,
        [Description("每页条数（默认100，最大500）")] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(visitId, settlementId, personnelNo, medicalRecordNo, inpatientOutpatientNo, insuranceType, medicalCategory, page, pageSize);

        return await _repository.QuerySettlementsAsync(filter, cancellationToken);
    }

    /// <summary>
    /// 根据查询条件自动获取全部数据并导出为 Excel 文件
    /// </summary>
    /// <param name="visitId">就诊ID（可选，精确匹配）</param>
    /// <param name="settlementId">结算ID（可选，精确匹配）</param>
    /// <param name="personnelNo">人员编号（可选，精确匹配）</param>
    /// <param name="medicalRecordNo">病历号（可选，精确匹配）</param>
    /// <param name="inpatientOutpatientNo">住院/门诊号（可选，精确匹配）</param>
    /// <param name="insuranceType">险种类型（可选，精确匹配）</param>
    /// <param name="medicalCategory">医疗类别（可选，精确匹配）</param>
    /// <param name="sheetName">工作表名称（默认 "YueHai结算数据"）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>
    /// 保存的 Excel 文件完整路径。用户可直接从文件管理器访问该路径获取文件。
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>这是完整流程的第三步。</b>
    /// 此方法内部自动获取全部数据，然后将所有数据导出为 Excel 文件，无需调用方手动拼接数据。
    /// </para>
    /// <para>
    /// <b>文件保存位置：</b>
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Windows: <c>%TEMP%\SettlementMcpServer\</c></description></item>
    ///   <item><description>Linux/macOS: <c>/tmp/SettlementMcpServer/</c></description></item>
    ///   <item><description>文件名格式: <c>YuehaiSettlementExport_yyyyMMddHHmmss_随机字符串.xlsx</c></description></item>
    /// </list>
    /// <para>
    /// <b>使用示例：</b>
    /// <code>
    /// // 直接调用步骤 3，传入与步骤 1/2 相同的查询条件即可
    /// var filePath = await ExportSettlementsToExcelAsync(
    ///     personnelNo: "P001",
    ///     sheetName: "P001结算数据");
    /// // filePath: "C:\Users\xxx\AppData\Local\Temp\SettlementMcpServer\YuehaiSettlementExport_20260617143025_abc123.xlsx"
    /// </code>
    /// </para>
    /// </remarks>
    [McpServerTool]
    [Description("根据查询条件获取YueHai结算数据，保存到本机临时文件夹并返回文件路径")]
    public async Task<string> ExportSettlementsToExcelAsync(
        [Description("就诊ID（可选）")] string? visitId = null,
        [Description("结算ID（可选）")] string? settlementId = null,
        [Description("人员编号（可选）")] string? personnelNo = null,
        [Description("病历号（可选）")] string? medicalRecordNo = null,
        [Description("住院/门诊号（可选）")] string? inpatientOutpatientNo = null,
        [Description("险种类型（可选）")] string? insuranceType = null,
        [Description("医疗类别（可选）")] string? medicalCategory = null,
        [Description("工作表名称（默认\"YueHai结算数据\"）")] string? sheetName = null,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(visitId, settlementId, personnelNo, medicalRecordNo, inpatientOutpatientNo, insuranceType, medicalCategory);

        _logger.LogInformation("步骤 3 导出YueHai结算数据 Excel，直接获取全部数据");

        var allResults = await _repository.QueryAllSettlementsAsync(filter, cancellationToken);

        if (allResults.Count == 0)
        {
            throw new InvalidOperationException("查询条件未匹配到任何数据，无法导出 Excel。请检查查询参数。");
        }

        _logger.LogInformation(
            "步骤 3 导出 Excel，获取全部数据 {Count} 条，工作表: {SheetName}",
            allResults.Count, sheetName ?? "YueHai结算数据");

        return await _excelExportService.ExportYuehaiSettlementsToExcelAsync(allResults, sheetName, cancellationToken);
    }

    /// <summary>
    /// 构建查询过滤器
    /// </summary>
    private static YuehaiSettlementQueryFilter BuildFilter(
        string? visitId,
        string? settlementId,
        string? personnelNo,
        string? medicalRecordNo,
        string? inpatientOutpatientNo,
        string? insuranceType,
        string? medicalCategory,
        int page = 1,
        int pageSize = 100)
    {
        return new YuehaiSettlementQueryFilter
        {
            VisitId = visitId,
            SettlementId = settlementId,
            PersonnelNo = personnelNo,
            MedicalRecordNo = medicalRecordNo,
            InpatientOutpatientNo = inpatientOutpatientNo,
            InsuranceType = insuranceType,
            MedicalCategory = medicalCategory,
            Page = page,
            PageSize = pageSize,
        };
    }
}
