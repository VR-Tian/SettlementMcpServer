using SettlementMcpServer.Models;

namespace SettlementMcpServer.Contracts;

/// <summary>
/// 粤海医保结算数据仓储接口
/// </summary>
public interface IYuehaiSettlementDataRepository
{
    /// <summary>
    /// 查询符合过滤条件的全部结算数据
    /// </summary>
    /// <param name="filter">查询条件</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>符合过滤条件的全部结算数据列表</returns>
    Task<IReadOnlyList<YuehaiSettlement>> QueryAllSettlementsAsync(
        YuehaiSettlementQueryFilter filter,
        CancellationToken cancellationToken = default);
}
