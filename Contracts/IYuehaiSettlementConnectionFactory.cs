using System.Data;

namespace SettlementMcpServer.Contracts;

/// <summary>
/// 粤海医保结算数据库连接工厂接口
/// </summary>
public interface IYuehaiSettlementConnectionFactory
{
    /// <summary>
    /// 创建新的数据库连接实例
    /// </summary>
    IDbConnection CreateConnection();
}
