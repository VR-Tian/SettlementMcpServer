namespace SettlementMcpServer.Contracts;

/// <summary>
/// 审核数据数据库连接工厂接口
/// </summary>
/// <remarks>
/// <para>
/// 继承自 <see cref="IDbConnectionFactory"/>，用于区分审核数据专用的数据库连接工厂。
/// 当系统中存在多个数据源时，通过不同的接口类型避免 DI 注册覆盖问题。
/// </para>
/// </remarks>
public interface IAuditDbConnectionFactory : IDbConnectionFactory
{
}
