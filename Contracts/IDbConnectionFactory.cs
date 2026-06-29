using System.Data;

namespace SettlementMcpServer.Contracts;

/// <summary>
/// 数据库连接工厂接口
/// </summary>
/// <remarks>
/// <para>
/// 负责创建和配置数据库连接实例。通过此接口抽象，仓储层不再依赖具体的数据库驱动类型。
/// </para>
/// <para>
/// <b>为什么需要连接工厂？</b>
/// </para>
/// <list type="bullet">
///   <item><description>解耦：仓储不需要引用 <c>Oracle.ManagedDataAccess.Client</c>，未来可无缝切换到 MySQL/PostgreSQL。</description></item>
///   <item><description>连接池管理：工厂内部使用连接池，避免每次查询都建立新连接的开销。</description></item>
///   <item><description>可测试性：测试时可注入 Mock 工厂返回内存数据库连接。</description></item>
/// </list>
/// </remarks>
public interface IDbConnectionFactory
{
    /// <summary>
    /// 创建新的数据库连接实例
    /// </summary>
    /// <remarks>
    /// <para>
    /// 每次调用此方法都会返回一个 <b>全新</b> 的连接实例。
    /// 连接池由 ADO.NET Provider 内部自动管理（如 Oracle 的 <c>Persist Security Info=True</c>），
    /// 因此调用方无需担心频繁创建连接的性能开销。
    /// </para>
    /// <para>
    /// <b>调用方责任：</b>
    /// 调用方必须通过 <c>await using</c> 确保连接被正确释放。
    /// </para>
    /// </remarks>
    /// <returns>未打开的数据库连接实例</returns>
    IDbConnection CreateConnection();
}
