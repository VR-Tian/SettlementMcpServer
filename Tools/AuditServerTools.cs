using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models;

namespace SettlementMcpServer.Tools;

/// <summary>
/// 医保审核服务工具
/// </summary>
/// <remarks>
/// <para>
/// 此类封装了医保审核相关的 MCP 工具方法，客户端可通过 MCP 协议调用这些工具。
/// </para>
/// <para>
/// <b>完整查询导出流程（解决 MCP 上下文长度限制）：</b>
/// </para>
/// <list type="number">
///   <item>
///     <b>步骤 1：获取总数</b> - 调用 <see cref="GetAuditedResultCountAsync"/>
///     获取符合查询条件的总记录数和分页元数据。
///   </item>
///   <item>
///     <b>步骤 2：循环请求</b> - 根据返回的 <c>TotalPages</c>，循环调用
///     <see cref="QueryAuditedResultsAsync"/> 每次传入不同的 <c>page</c> 参数获取数据。
///   </item>
///   <item>
///     <b>步骤 3：导出 Excel</b> - 调用 <see cref="ExportAuditedResultsToExcelAsync"/>，
///     内部自动执行步骤 1+2 获取全部数据后导出为 Excel 文件。
///   </item>
/// </list>
/// </remarks>
internal class AuditServerTools
{
    private readonly IAuditDataRepository _auditDataRepository;
    private readonly IExcelExportService _excelExportService;
    private readonly ILogger<AuditServerTools> _logger;

    /// <summary>
    /// 初始化医保审核工具实例
    /// </summary>
    /// <param name="auditDataRepository">审核数据仓储（由 DI 容器自动注入）</param>
    /// <param name="excelExportService">Excel 导出服务（由 DI 容器自动注入）</param>
    /// <param name="logger">日志记录器（由 DI 容器自动注入，可选）</param>
    public AuditServerTools(
        IAuditDataRepository auditDataRepository,
        IExcelExportService excelExportService,
        ILogger<AuditServerTools>? logger = null)
    {
        _auditDataRepository = auditDataRepository ?? throw new ArgumentNullException(nameof(auditDataRepository));
        _excelExportService = excelExportService ?? throw new ArgumentNullException(nameof(excelExportService));
        _logger = logger ?? NullLogger<AuditServerTools>.Instance;
    }

    /// <summary>
    /// 获取符合查询条件的总记录数和分页元数据
    /// </summary>
    /// <param name="medicalRecordNo">病案号（可选，精确匹配）</param>
    /// <param name="hospitalCode">医院编码（可选，精确匹配）</param>
    /// <param name="insuredNo">参保人号（可选，精确匹配）</param>
    /// <param name="ruleCode">规则编码（可选，精确匹配）</param>
    /// <param name="pageSize">每页条数，默认 100，最大 500</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>分页元数据（总记录数、总页数、当前页等）</returns>
    /// <remarks>
    /// <para>
    /// <b>这是分页查询的第一步。</b>
    /// 客户端先调用此方法获取总记录数，然后根据返回的 <c>TotalPages</c>
    /// 循环调用 <see cref="QueryAuditedResultsAsync"/> 获取每页数据。
    /// </para>
    /// <para>
    /// <b>使用示例：</b>
    /// <code>
    /// // 步骤 1：获取总数
    /// var pagination = await GetAuditedResultCountAsync(hospitalCode: "H001", pageSize: 100);
    /// // pagination.TotalCount = 1250, pagination.TotalPages = 13
    ///
    /// // 步骤 2：循环请求每一页
    /// for (int page = 1; page &lt;= pagination.TotalPages; page++)
    /// {
    ///     var results = await QueryAuditedResultsAsync(hospitalCode: "H001", page: page, pageSize: 100);
    ///     // 处理 results...
    /// }
    /// </code>
    /// </para>
    /// </remarks>
    [McpServerTool]
    [Description("获取审核数据查询的总记录数和分页元数据，用于计算需要请求的页数")]
    public async Task<PaginationMetadata> GetAuditedResultCountAsync(
        [Description("病案号（可选）")] string? medicalRecordNo = null,
        [Description("医院编码（可选）")] string? hospitalCode = null,
        [Description("参保人号（可选）")] string? insuredNo = null,
        [Description("规则编码（可选）")] string? ruleCode = null,
        [Description("每页条数（默认100，最大500）")] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(medicalRecordNo, hospitalCode, insuredNo, ruleCode);

        var totalCount = await _auditDataRepository.CountAuditedResultsAsync(filter, cancellationToken);

        return new PaginationMetadata
        {
            TotalCount = totalCount,
            PageSize = pageSize,
            CurrentPage = 1,
        };
    }

    /// <summary>
    /// 分页查询医保审核结果明细数据
    /// </summary>
    /// <param name="medicalRecordNo">病案号（可选，精确匹配）</param>
    /// <param name="hospitalCode">医院编码（可选，精确匹配）</param>
    /// <param name="insuredNo">参保人号（可选，精确匹配）</param>
    /// <param name="ruleCode">规则编码（可选，精确匹配）</param>
    /// <param name="page">页码，从 1 开始，默认 1</param>
    /// <param name="pageSize">每页条数，默认 100，最大 500</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前页的审核结果明细列表</returns>
    /// <remarks>
    /// <para>
    /// <b>这是分页查询的第二步。</b>
    /// 调用此方法前应先通过 <see cref="GetAuditedResultCountAsync"/> 获取总页数。
    /// </para>
    /// <para>
    /// <b>分页说明：</b>
    /// 每次请求返回的数据量受 <c>pageSize</c> 控制，确保不超出 MCP 上下文长度限制。
    /// 建议根据实际上下文容量调整 <c>pageSize</c>（默认 100 条）。
    /// </para>
    /// </remarks>
    [McpServerTool]
    [Description("分页查询审核结果明细数据，需配合 GetAuditedResultCountAsync 使用")]
    public async Task<IReadOnlyList<AuditedResult>> QueryAuditedResultsAsync(
        [Description("病案号（可选）")] string? medicalRecordNo = null,
        [Description("医院编码（可选）")] string? hospitalCode = null,
        [Description("参保人号（可选）")] string? insuredNo = null,
        [Description("规则编码（可选）")] string? ruleCode = null,
        [Description("页码，从1开始（默认1）")] int page = 1,
        [Description("每页条数（默认100，最大500）")] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(medicalRecordNo, hospitalCode, insuredNo, ruleCode, page, pageSize);

        return await _auditDataRepository.QueryAuditedResultsAsync(filter, cancellationToken);
    }

    /// <summary>
    /// 根据查询条件自动获取全部数据并导出为 Excel 文件
    /// </summary>
    /// <param name="medicalRecordNo">病案号（可选，精确匹配）</param>
    /// <param name="hospitalCode">医院编码（可选，精确匹配）</param>
    /// <param name="insuredNo">参保人号（可选，精确匹配）</param>
    /// <param name="ruleCode">规则编码（可选，精确匹配）</param>
    /// <param name="sheetName">工作表名称（默认 "审核数据"）</param>
    /// <param name="pageSize">内部分页每页条数，默认 100，最大 500</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>
    /// 保存的 Excel 文件完整路径。用户可直接从文件管理器访问该路径获取文件。
    /// </returns>
    /// <remarks>
    /// <para>
    /// <b>这是完整流程的第三步。</b>
    /// 此方法内部自动执行步骤 1（获取总数）+ 步骤 2（循环分页获取全部数据），
    /// 然后将所有数据导出为 Excel 文件，无需调用方手动拼接数据。
    /// </para>
    /// <para>
    /// <b>内部流程：</b>
    /// </para>
    /// <list type="number">
    ///   <item><description>调用仓储 COUNT 查询获取总记录数</description></item>
    ///   <item><description>计算总页数，循环分页查询获取所有数据</description></item>
    ///   <item><description>将全部数据导出为 Excel 文件保存到临时文件夹</description></item>
    /// </list>
    /// <para>
    /// <b>文件保存位置：</b>
    /// </para>
    /// <list type="bullet">
    ///   <item><description>Windows: <c>%TEMP%\SettlementMcpServer\</c></description></item>
    ///   <item><description>Linux/macOS: <c>/tmp/SettlementMcpServer/</c></description></item>
    ///   <item><description>文件名格式: <c>AuditExport_yyyyMMddHHmmss_随机字符串.xlsx</c></description></item>
    /// </list>
    /// <para>
    /// <b>使用示例：</b>
    /// <code>
    /// // 直接调用步骤 3，传入与步骤 1/2 相同的查询条件即可
    /// var filePath = await ExportAuditedResultsToExcelAsync(
    ///     hospitalCode: "H001",
    ///     sheetName: "H001审核数据");
    /// // filePath: "C:\Users\xxx\AppData\Local\Temp\SettlementMcpServer\AuditExport_20260617143025_abc123.xlsx"
    /// </code>
    /// </para>
    /// </remarks>
    [McpServerTool]
    [Description("根据查询条件获取医保审核结果，保存到本机临时文件夹并返回文件路径")]
    public async Task<string> ExportAuditedResultsToExcelAsync(
        [Description("病案号（可选）")] string? medicalRecordNo = null,
        [Description("医院编码（可选）")] string? hospitalCode = null,
        [Description("参保人号（可选）")] string? insuredNo = null,
        [Description("规则编码（可选）")] string? ruleCode = null,
        [Description("工作表名称（默认\"审核数据\"）")] string? sheetName = null,
        CancellationToken cancellationToken = default)
    {
        // 直接查询全部数据（不分页）
        var filter = BuildFilter(medicalRecordNo, hospitalCode, insuredNo, ruleCode);
        var allResults = await _auditDataRepository.QueryAllAuditedResultsAsync(filter, cancellationToken);

        if (allResults.Count == 0)
        {
            throw new InvalidOperationException("查询条件未匹配到任何数据，无法导出 Excel。请检查查询参数。");
        }

        _logger.LogInformation(
            "步骤 3 导出 Excel，查询到全部数据 {Count} 条，工作表: {SheetName}",
            allResults.Count, sheetName ?? "审核数据");

        return await _excelExportService.ExportAuditedResultsToExcelAsync(allResults, sheetName, cancellationToken);
    }

    /// <summary>
    /// 构建查询过滤器
    /// </summary>
    /// <param name="medicalRecordNo">病案号（可选）</param>
    /// <param name="hospitalCode">医院编码（可选）</param>
    /// <param name="insuredNo">参保人号（可选）</param>
    /// <param name="ruleCode">规则编码（可选）</param>
    /// <param name="page">页码（默认 1）</param>
    /// <param name="pageSize">每页条数（默认 100）</param>
    /// <returns>查询过滤器实例</returns>
    /// <remarks>
    /// <para>
    /// 将各工具方法中重复的过滤器构建逻辑集中到此处，
    /// 确保步骤 1/2/3 使用完全一致的过滤条件构建方式。
    /// </para>
    /// </remarks>
    private static AuditedResultQueryFilter BuildFilter(
        string? medicalRecordNo,
        string? hospitalCode,
        string? insuredNo,
        string? ruleCode,
        int page = 1,
        int pageSize = 100)
    {
        return new AuditedResultQueryFilter
        {
            MedicalRecordNo = medicalRecordNo,
            HospitalCode = hospitalCode,
            InsuredNo = insuredNo,
            RuleCode = ruleCode,
            Page = page,
            PageSize = pageSize,
        };
    }

}
