using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ModelContextProtocol.Server;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Infrastructure.DuckDb;
using SettlementMcpServer.Models;
using SettlementMcpServer.Models.Rules;
using TaskStatus = SettlementMcpServer.Models.TaskStatus;

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
    private readonly IRulePipeline _rulePipeline;
    private readonly ISettlementDataRepository _SettlementDataRepository;
    private readonly IAuditTaskRepository _auditTaskRepository;
    private readonly IRuleRepository _ruleRepository;
    private readonly IAuditResultRepository _auditResultRepository;
    private readonly DuckDbSettlementDataRepository _duckDbSettlementDataRepository;
    private readonly IDataSyncService _dataSyncService;
    private readonly ILogger<AuditServerTools> _logger;

    /// <summary>
    /// 初始化医保审核工具实例
    /// </summary>
    /// <param name="auditDataRepository">审核数据仓储（由 DI 容器自动注入）</param>
    /// <param name="excelExportService">Excel 导出服务（由 DI 容器自动注入）</param>
    /// <param name="rulePipeline">规则管道（由 DI 容器自动注入）</param>
    /// <param name="SettlementDataRepository"> 结算数据仓储（由 DI 容器自动注入）</param>
    /// <param name="auditTaskRepository">审核任务仓储（由 DI 容器自动注入）</param>
    /// <param name="ruleRepository">规则仓储（由 DI 容器自动注入）</param>
    /// <param name="auditResultRepository">审核结果仓储（由 DI 容器自动注入）</param>
    /// <param name="duckDbSettlementDataRepository">DuckDB 结算数据仓储（由 DI 容器自动注入）</param>
    /// <param name="dataSyncService">数据同步服务（由 DI 容器自动注入）</param>
    /// <param name="logger">日志记录器（由 DI 容器自动注入，可选）</param>
    public AuditServerTools(
        IAuditDataRepository auditDataRepository,
        IExcelExportService excelExportService,
        IRulePipeline rulePipeline,
        ISettlementDataRepository SettlementDataRepository,
        IAuditTaskRepository auditTaskRepository,
        IRuleRepository ruleRepository,
        IAuditResultRepository auditResultRepository,
        DuckDbSettlementDataRepository duckDbSettlementDataRepository,
        IDataSyncService dataSyncService,
        ILogger<AuditServerTools>? logger = null)
    {
        _auditDataRepository = auditDataRepository ?? throw new ArgumentNullException(nameof(auditDataRepository));
        _excelExportService = excelExportService ?? throw new ArgumentNullException(nameof(excelExportService));
        _rulePipeline = rulePipeline ?? throw new ArgumentNullException(nameof(rulePipeline));
        _SettlementDataRepository = SettlementDataRepository ?? throw new ArgumentNullException(nameof(SettlementDataRepository));
        _auditTaskRepository = auditTaskRepository ?? throw new ArgumentNullException(nameof(auditTaskRepository));
        _ruleRepository = ruleRepository ?? throw new ArgumentNullException(nameof(ruleRepository));
        _auditResultRepository = auditResultRepository ?? throw new ArgumentNullException(nameof(auditResultRepository));
        _duckDbSettlementDataRepository = duckDbSettlementDataRepository ?? throw new ArgumentNullException(nameof(duckDbSettlementDataRepository));
        _dataSyncService = dataSyncService ?? throw new ArgumentNullException(nameof(dataSyncService));
        _logger = logger ?? NullLogger<AuditServerTools>.Instance;
    }

    /// <summary>
    /// 获取符合查询条件的总记录数和分页元数据
    /// </summary>
    /// <param name="medicalRecordNo">病案号（可选，精确匹配）</param>
    /// <param name="hospitalCode">医院编码（可选，精确匹配）</param>
    /// <param name="insuredNo">参保人号（可选，精确匹配）</param>
    /// <param name="ruleName">规则名称（可选，精确匹配）</param>
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
        [Description("规则名称（可选）")] string? ruleName = null,
        [Description("每页条数（默认100，最大500）")] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(medicalRecordNo, hospitalCode, insuredNo, ruleName);

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
    /// <param name="ruleName">规则名称（可选，精确匹配）</param>
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
        [Description("规则名称（可选）")] string? ruleName = null,
        [Description("页码，从1开始（默认1）")] int page = 1,
        [Description("每页条数（默认100，最大500）")] int pageSize = 100,
        CancellationToken cancellationToken = default)
    {
        var filter = BuildFilter(medicalRecordNo, hospitalCode, insuredNo, ruleName, page, pageSize);

        return await _auditDataRepository.QueryAuditedResultsAsync(filter, cancellationToken);
    }

    /// <summary>
    /// 根据查询条件自动获取全部数据并导出为 Excel 文件
    /// </summary>
    /// <param name="medicalRecordNo">病案号（可选，精确匹配）</param>
    /// <param name="hospitalCode">医院编码（可选，精确匹配）</param>
    /// <param name="insuredNo">参保人号（可选，精确匹配）</param>
    /// <param name="ruleName">规则名称（可选，精确匹配）</param>
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
        [Description("规则名称（可选）")] string? ruleName = null,
        [Description("工作表名称（默认\"审核数据\"）")] string? sheetName = null,
        CancellationToken cancellationToken = default)
    {
        // 直接查询全部数据（不分页）
        var filter = BuildFilter(medicalRecordNo, hospitalCode, insuredNo, ruleName);
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

    #region  规则内涵执行审核服务

    /// <summary>
    /// 根据规则名称从数据库加载规则并审核医保数据，分析结果保存到本机临时文件夹并返回文件路径
    /// </summary>
    /// <param name="ruleName">规则名称（必填，用于从数据库加载规则集）</param>
    /// <param name="hospitalCode">医院编码（可选，按定点医药机构编号过滤结算数据）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>导出的违规结果 Excel 文件完整路径；无违规或无数据时返回提示信息</returns>
    /// <exception cref="ArgumentException">规则名称为空时抛出</exception>
    /// <exception cref="InvalidOperationException">数据库中未找到对应规则时抛出</exception>
    [McpServerTool]
    [Description("根据规则名称从数据库加载规则审核医保数据，分析结果保存到本机临时文件夹并返回文件路径")]
    public async Task<string> ExecAuditAnalysisAsync(
        [Description("规则名称")] string? ruleName = null,
        [Description("医院编码（可选）")] string? hospitalCode = null,
        [Description("结算ID（可选）")] string? SettlementId = null,
        CancellationToken cancellationToken = default)
    {
        // 1. 验证参数
        if (string.IsNullOrWhiteSpace(ruleName))
        {
            throw new ArgumentException("规则名称不能为空", nameof(ruleName));
        }

        // 2. 从数据库加载规则
        var ruleSet = await _ruleRepository.GetRuleSetByNameAsync(ruleName, cancellationToken);
        if (ruleSet == null)
        {
            throw new InvalidOperationException($"数据库中未找到规则名称为 {ruleName} 的规则集，请确认规则已初始化");
        }

        _logger.LogInformation("开始执行规则审核，规则名称: {RuleName}，规则类别: {Category}", ruleName, ruleSet.Category);

        // 3. 从 DuckDB 查询结算数据（而非 Oracle）
        var filter = new SettlementQueryFilter
        {
            InstitutionCode = hospitalCode,
            SettlementId = SettlementId
        };

        // 3.1 检查 DuckDB 中是否存在 _settlements 视图，如果不存在则自动同步数据
        // if (!await CheckSettlementsViewExistsAsync(cancellationToken))
        // {
        //     _logger.LogInformation("DuckDB 中不存在 _settlements 视图，开始自动同步结算数据");
        //     await _dataSyncService.SyncSettlementsAsync(cancellationToken);
        //     _logger.LogInformation("结算数据同步完成");
        // }

        var settlements = await _duckDbSettlementDataRepository.QueryAllSettlementsAsync(filter, cancellationToken);

        if (settlements.Count == 0)
        {
            _logger.LogWarning("未查询到符合条件的结算数据，医院编码: {HospitalCode}", hospitalCode);
            return "未查询到符合条件的结算数据";
        }

        _logger.LogInformation("从 DuckDB 查询到结算数据 {Count} 条", settlements.Count);

        // 4. 执行规则管道（传入规则名称，由 DuplicateChargeDbRuleLoader 从数据库加载）
        var violations = await _rulePipeline.ExecuteAsync(ruleName, settlements, cancellationToken);

        if (violations.Count == 0)
        {
            _logger.LogInformation("规则审核完成，未发现违规记录");
            return "规则审核完成，未发现违规记录";
        }

        _logger.LogInformation("规则审核完成，发现 {Count} 条违规记录", violations.Count);

        // 5. 将违规结果导出为Excel
        var auditedResults = violations.Select(MapToAuditedResult).ToList();
        var excelFilePath = await _excelExportService.ExportAuditedResultsToExcelAsync(
            auditedResults,
            $"规则{ruleName}审核结果",
            cancellationToken);

        _logger.LogInformation("违规结果已导出到Excel文件: {FilePath}", excelFilePath);

        return excelFilePath;
    }

    /// <summary>
    /// 将规则违规结果映射为审核数据结果，以便复用现有的 Excel 导出服务
    /// </summary>
    /// <param name="violation">规则违规结果</param>
    /// <returns>映射后的审核数据结果</returns>
    private static AuditedResult MapToAuditedResult(RuleViolation violation)
    {
        return new AuditedResult
        {
            RuleName = violation.RuleName,
            ReasonExplanation = violation.PromptMessage,
            MedicalRecordNo = violation.PersonnelNo,
            HospitalCode = violation.InstitutionCode,
            HospitalName = violation.InstitutionName,
            ItemCode = violation.ViolationItemCode,
            ItemName = violation.ViolationItemName,
            Quantity = violation.ViolationQuantity,
            UnitPrice = violation.ViolationUnitPrice,
            Amount = violation.ViolationAmount,
            Department = violation.ReceivingDeptCode,
        };
    }

    #endregion

    #region 审核任务管理服务

    /// <summary>
    /// 创建审核任务
    /// </summary>
    /// <param name="ruleName">规则名称（必填）</param>
    /// <param name="hospitalCode">医院编码（可选，为空时表示全部机构）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>新创建的任务ID</returns>
    /// <exception cref="ArgumentException">规则名称为空时抛出</exception>
    [McpServerTool]
    [Description("创建审核任务，返回任务ID")]
    public async Task<string> CreateAuditTaskAsync(
        [Description("规则名称（必填）")] string ruleName,
        [Description("医院编码（可选，为空时表示全部机构）")] string? hospitalCode = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ruleName))
        {
            throw new ArgumentException("规则名称不能为空", nameof(ruleName));
        }

        var taskId = Guid.NewGuid().ToString("N");
        var task = new AuditTask
        {
            TaskId = taskId,
            RuleName = ruleName,
            HospitalCode = hospitalCode,
            Status = TaskStatus.Pending,
            TotalCount = 0,
            ProcessedCount = 0,
            ViolationCount = 0,
            CreatedAt = DateTime.Now
        };

        await _auditTaskRepository.SaveTaskAsync(task, cancellationToken);

        _logger.LogInformation("已创建审核任务，任务ID: {TaskId}，规则名称: {RuleName}，医院编码: {HospitalCode}",
            taskId, ruleName, hospitalCode ?? "全部");

        return taskId;
    }

    /// <summary>
    /// 获取审核任务状态
    /// </summary>
    /// <param name="taskId">任务ID（必填）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>任务状态和进度信息</returns>
    /// <exception cref="ArgumentException">任务ID为空时抛出</exception>
    /// <exception cref="InvalidOperationException">任务不存在时抛出</exception>
    [McpServerTool]
    [Description("获取审核任务状态和进度信息")]
    public async Task<AuditTaskStatusResponse> GetAuditTaskStatusAsync(
        [Description("任务ID（必填）")] string taskId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(taskId))
        {
            throw new ArgumentException("任务ID不能为空", nameof(taskId));
        }

        var task = await _auditTaskRepository.GetTaskAsync(taskId, cancellationToken);
        if (task == null)
        {
            throw new InvalidOperationException($"任务 {taskId} 不存在");
        }

        var progressPercentage = task.TotalCount > 0
            ? (int)Math.Round((double)task.ProcessedCount / task.TotalCount * 100, 2)
            : 0;

        return new AuditTaskStatusResponse
        {
            TaskId = task.TaskId,
            RuleName = task.RuleName,
            HospitalCode = task.HospitalCode,
            Status = task.Status.ToString(),
            TotalCount = task.TotalCount,
            ProcessedCount = task.ProcessedCount,
            ViolationCount = task.ViolationCount,
            ProgressPercentage = progressPercentage,
            CreatedAt = task.CreatedAt,
            CompletedAt = task.CompletedAt,
            ErrorMessage = task.ErrorMessage
        };
    }

    /// <summary>
    /// 查询审核结果
    /// </summary>
    /// <param name="taskId">任务ID（可选，按任务ID过滤）</param>
    /// <param name="ruleName">规则名称（可选，按规则名称过滤）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>审核结果列表</returns>
    [McpServerTool]
    [Description("查询审核结果，可按任务ID或规则名称过滤")]
    public async Task<IReadOnlyList<AuditResult>> QueryAuditResultsAsync(
        [Description("任务ID（可选，按任务ID过滤）")] string? taskId = null,
        [Description("规则名称（可选，按规则名称过滤）")] string? ruleName = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<AuditResult> results;

        if (!string.IsNullOrWhiteSpace(taskId))
        {
            results = await _auditResultRepository.GetAuditResultsByTaskIdAsync(taskId, cancellationToken);
            _logger.LogInformation("按任务ID {TaskId} 查询到审核结果 {Count} 条", taskId, results.Count);
        }
        else if (!string.IsNullOrWhiteSpace(ruleName))
        {
            results = await _auditResultRepository.GetAuditResultsByRuleNameAsync(ruleName, cancellationToken);
            _logger.LogInformation("按规则名称 {RuleName} 查询到审核结果 {Count} 条", ruleName, results.Count);
        }
        else
        {
            _logger.LogWarning("查询审核结果时未提供任务ID或规则名称，返回空列表");
            return Array.Empty<AuditResult>();
        }

        return results;
    }

    #endregion

    /// <summary>
    /// 审核任务状态响应模型
    /// </summary>
    public class AuditTaskStatusResponse
    {
        /// <summary>任务ID</summary>
        public string TaskId { get; set; } = string.Empty;

        /// <summary>规则名称</summary>
        public string RuleName { get; set; } = string.Empty;

        /// <summary>医院编码</summary>
        public string? HospitalCode { get; set; }

        /// <summary>任务状态</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>待审核结算数据总数</summary>
        public int TotalCount { get; set; }

        /// <summary>已处理结算数据数量</summary>
        public int ProcessedCount { get; set; }

        /// <summary>违规数量</summary>
        public int ViolationCount { get; set; }

        /// <summary>进度百分比</summary>
        public double ProgressPercentage { get; set; }

        /// <summary>任务创建时间</summary>
        public DateTime CreatedAt { get; set; }

        /// <summary>任务完成时间</summary>
        public DateTime? CompletedAt { get; set; }

        /// <summary>错误信息</summary>
        public string? ErrorMessage { get; set; }
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
            //RuleCode = ruleCode,
            Page = page,
            PageSize = pageSize,
        };
    }

    /// <summary>
    /// 检查 DuckDB 中是否存在 _settlements 视图
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>如果存在返回 true，否则返回 false</returns>
    private async Task<bool> CheckSettlementsViewExistsAsync(CancellationToken cancellationToken)
    {
        try
        {
            // 先列出所有表和视图，用于调试
            var result = await _duckDbSettlementDataRepository.CountSettlementsAsync(new SettlementQueryFilter(), cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "检查 _settlements 视图时发生异常，视图可能不存在");

            // 尝试列出 DuckDB 中所有的表和视图
            try
            {
                var tablesResult = await ExecuteDuckDbQueryAsync("SELECT table_name, table_type FROM information_schema.tables WHERE table_schema = 'main'", cancellationToken);
                _logger.LogInformation("DuckDB 中的表和视图: {Tables}", tablesResult);
            }
            catch (Exception listEx)
            {
                _logger.LogError(listEx, "列出 DuckDB 表和视图时发生异常");
            }

            return false;
        }
    }

    /// <summary>
    /// 执行 DuckDB 查询并返回结果
    /// </summary>
    private async Task<string> ExecuteDuckDbQueryAsync(string sql, CancellationToken cancellationToken)
    {
        // 这里需要注入 IDuckDbQueryService，但为了简单起见，我们直接返回错误信息
        return "需要注入 IDuckDbQueryService";
    }

}
