namespace SettlementMcpServer.Models;

/// <summary>
/// 通用分页查询结果元数据
/// </summary>
/// <remarks>
/// <para>
/// 包含当前查询条件下的总记录数、总页数、当前页码等信息，客户端据此计算需要循环请求的页数。
/// </para>
/// <para>
/// <b>使用示例：</b>
/// <code>
/// // 获取总数
/// var pagination = await GetCountAsync(hospitalCode: "H001");
/// // pagination.TotalCount = 1250, pagination.TotalPages = 13 (pageSize=100)
///
/// // 循环请求每一页
/// for (int page = 1; page &lt;= pagination.TotalPages; page++)
/// {
///     var results = await QueryAsync(hospitalCode: "H001", page: page, pageSize: 100);
///     // 处理 results...
/// }
/// </code>
/// </para>
/// </remarks>
public record PaginationMetadata
{
    /// <summary>
    /// 符合查询条件的总记录数
    /// </summary>
    public int TotalCount { get; init; }

    /// <summary>
    /// 每页条数
    /// </summary>
    public int PageSize { get; init; }

    /// <summary>
    /// 总页数
    /// </summary>
    /// <remarks>计算公式: (TotalCount + PageSize - 1) / PageSize</remarks>
    public int TotalPages => (TotalCount + PageSize - 1) / PageSize;

    /// <summary>
    /// 当前页码（用于确认请求的参数）
    /// </summary>
    public int CurrentPage { get; init; }

    /// <summary>
    /// 是否还有下一页
    /// </summary>
    public bool HasNextPage => CurrentPage < TotalPages;

    /// <summary>
    /// 是否还有上一页
    /// </summary>
    public bool HasPreviousPage => CurrentPage > 1;
}
