using SettlementMcpServer.Models;

namespace SettlementMcpServer.Contracts;

/// <summary>
/// 审核数据仓储接口
/// </summary>
/// <remarks>
/// <para>
/// 定义了审核数据查询的契约。具体的数据库实现（如 Oracle、MySQL、PostgreSQL）
/// 需要实现此接口并在 DI 容器中注册。
/// </para>
/// <para>
/// 接口设计要点：
/// <list type="bullet">
///   <item><description>方法参数使用 <see cref="Models.AuditedResultQueryFilter"/> 封装所有查询条件，避免方法签名膨胀。</description></item>
///   <item><description>返回值使用 <see cref="System.Collections.Generic.IReadOnlyList{T}"/> 而非 <see cref="System.Collections.Generic.List{T}"/>，防止调用方意外修改查询结果。</description></item>
///   <item><description>所有方法接受 <see cref="CancellationToken"/> 参数，支持异步操作取消。</description></item>
/// </list>
/// </para>
/// </remarks>
public interface IAuditDataRepository
{
    /// <summary>
    /// 查询审核结果明细数据（分页）
    /// </summary>
    /// <param name="filter">查询条件（包含页码和每页条数）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前页的审核结果列表</returns>
    Task<IReadOnlyList<AuditedResult>> QueryAuditedResultsAsync(
        AuditedResultQueryFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询符合过滤条件的全部审核结果数据（不分页）
    /// </summary>
    /// <param name="filter">查询条件（仅使用过滤字段，忽略分页参数）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>全部符合条件的审核结果列表</returns>
    /// <remarks>
    /// 此方法用于 Excel 导出等需要一次性获取全部数据的场景，
    /// 直接执行 <c>SELECT * FROM 表 WHERE 条件</c>，无 ROWNUM 分页。
    /// </remarks>
    Task<IReadOnlyList<AuditedResult>> QueryAllAuditedResultsAsync(
        AuditedResultQueryFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取符合查询条件的总记录数
    /// </summary>
    /// <param name="filter">查询条件（仅使用过滤字段，忽略分页参数）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>总记录数</returns>
    /// <remarks>
    /// 此方法用于分页查询的第一步，客户端据此计算总页数，
    /// 然后循环调用 <see cref="QueryAuditedResultsAsync"/> 获取所有数据。
    /// </remarks>
    Task<int> CountAuditedResultsAsync(
        AuditedResultQueryFilter filter,
        CancellationToken cancellationToken = default);
}
