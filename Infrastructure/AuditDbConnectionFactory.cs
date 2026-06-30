using Oracle.ManagedDataAccess.Client;
using SettlementMcpServer.Contracts;

namespace SettlementMcpServer.Infrastructure;

/// <summary>
/// 审核数据数据库连接工厂实现
/// </summary>
/// <remarks>
/// <para>
/// 实现 <see cref="IAuditDbConnectionFactory"/> 接口，用于创建审核数据专用的数据库连接。
/// 当系统中存在多个数据源时，通过不同的连接工厂类型避免 DI 注册覆盖问题。
/// </para>
/// </remarks>
public sealed class AuditDbConnectionFactory : IAuditDbConnectionFactory
{
    private readonly string? _connectionString;
    private readonly string? _environmentVariableName;

    /// <summary>
    /// 方式 1：直接传入连接字符串（适用于启动时已确定的情况）
    /// </summary>
    public AuditDbConnectionFactory(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// 方式 2：传入环境变量名，每次创建连接时动态读取（支持运行时修改配置）
    /// </summary>
    public AuditDbConnectionFactory(string environmentVariableName, bool fromEnvironment)
    {
        _environmentVariableName = environmentVariableName ?? throw new ArgumentNullException(nameof(environmentVariableName));
    }

    /// <inheritdoc />
    public System.Data.IDbConnection CreateConnection()
    {
        // 优先使用直接传入的连接字符串，如果没有，则通过环境变量名动态获取
        var connectionString = _connectionString
            ?? Environment.GetEnvironmentVariable(_environmentVariableName!);

        if (string.IsNullOrEmpty(connectionString))
        {
            throw new InvalidOperationException($"未配置审核数据库连接字符串，请设置环境变量 {_environmentVariableName}");
        }

        return new OracleConnection(connectionString);
    }
}
