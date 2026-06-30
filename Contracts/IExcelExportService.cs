using SettlementMcpServer.Models;

namespace SettlementMcpServer.Contracts;

/// <summary>
/// Excel 导出服务接口
/// </summary>
/// <remarks>
/// <para>
/// 提供将审核数据导出为 Excel 文件的能力。通过接口抽象，未来可替换为其他
/// Excel 组件（如 EPPlus、ClosedXML）而不影响上层调用方。
/// </para>
/// </remarks>
public interface IExcelExportService
{
    /// <summary>
    /// 将审核数据集合导出为 Excel 文件并保存到本机临时文件夹
    /// </summary>
    /// <param name="data">审核数据集合</param>
    /// <param name="sheetName">工作表名称（默认 "审核数据"）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>保存的 Excel 文件完整路径</returns>
    /// <remarks>
    /// <para>
    /// <b>文件保存位置：</b>
    /// </para>
    /// <list type="bullet">
    ///   <item><description>使用系统临时文件夹（通过 <see cref="Path.GetTempPath()"/> 获取）</description></item>
    ///   <item><description>Windows: <c>%TEMP%\SettlementMcpServer\</c></description></item>
    ///   <item><description>Linux/macOS: <c>/tmp/SettlementMcpServer/</c></description></item>
    ///   <item><description>文件名格式: <c>AuditExport_yyyyMMddHHmmss_随机字符串.xlsx</c></description></item>
    /// </list>
    /// <para>
    /// <b>为什么返回文件路径而非 Base64？</b>
    /// </para>
    /// <list type="bullet">
    ///   <item><description>MCP 服务通过 stdio 通信，Base64 编码大数据量会导致传输中断。</description></item>
    ///   <item><description>文件路径字符串长度固定且小，不会造成传输问题。</description></item>
    ///   <item><description>用户可直接从文件管理器访问临时文件夹获取文件。</description></item>
    /// </list>
    /// </remarks>
    Task<string> ExportAuditedResultsToExcelAsync(
        IEnumerable<AuditedResult> data,
        string? sheetName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 将YueHai结算数据集合导出为 Excel 文件并保存到本机临时文件夹
    /// </summary>
    /// <param name="data">YueHai结算数据集合</param>
    /// <param name="sheetName">工作表名称（默认 "YueHai结算数据"）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>保存的 Excel 文件完整路径</returns>
    Task<string> ExportYuehaiSettlementsToExcelAsync(
        IEnumerable<YuehaiSettlement> data,
        string? sheetName = null,
        CancellationToken cancellationToken = default);
}
