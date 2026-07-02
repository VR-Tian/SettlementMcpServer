namespace SettlementMcpServer.Contracts;

/// <summary>
/// 分页查询参数接口
/// </summary>
/// <remarks>
/// 所有包含分页参数的查询过滤器都应实现此接口，
/// 以便 <see cref="Infrastructure.OracleRepositoryBase{T}"/> 泛型基类统一处理分页逻辑。
/// </remarks>
public interface IPagedQuery
{
    /// <summary>
    /// 页码，从 1 开始
    /// </summary>
    int Page { get; }

    /// <summary>
    /// 每页条数
    /// </summary>
    int PageSize { get; }
}
