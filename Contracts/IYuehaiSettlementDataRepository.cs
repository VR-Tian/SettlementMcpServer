using SettlementMcpServer.Models;

namespace SettlementMcpServer.Contracts;

/// <summary>
/// 医保结算数据仓储接口
/// </summary>
public interface ISettlementDataRepository
{
    /// <summary>
    /// 查询符合过滤条件的全部结算数据
    /// </summary>
    /// <param name="filter">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>符合过滤条件的全部结算数据列表</returns>
    Task<IReadOnlyList<Settlement>> QueryAllSettlementsAsync(
        SettlementQueryFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 查询符合过滤条件的结算数据总数
    /// </summary>
    /// <param name="filter">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>符合条件的记录总数</returns>
    Task<int> CountSettlementsAsync(
        SettlementQueryFilter filter,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 分页查询符合过滤条件的结算数据
    /// </summary>
    /// <param name="filter">查询条件（包含分页参数）</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>当前页的结算数据列表</returns>
    Task<IReadOnlyList<Settlement>> QuerySettlementsAsync(
        SettlementQueryFilter filter,
        CancellationToken cancellationToken = default);
}
