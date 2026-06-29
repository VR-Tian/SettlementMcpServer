using Oracle.ManagedDataAccess.Client;
using SettlementMcpServer.Contracts;

namespace SettlementMcpServer.Infrastructure;

/// <summary>
/// Oracle 数据库连接工厂实现
/// </summary>
public sealed class OracleDbConnectionFactory : IDbConnectionFactory, IYuehaiSettlementConnectionFactory
{
    private readonly string? _connectionString;
    private readonly string? _environmentVariableName;

    /// <summary>
    /// 方式 1：直接传入连接字符串（适用于启动时已确定的情况）
    /// </summary>
    public OracleDbConnectionFactory(string connectionString)
    {
        _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
    }

    /// <summary>
    /// 方式 2：传入环境变量名，每次创建连接时动态读取（支持运行时修改配置）
    /// </summary>
    public OracleDbConnectionFactory(string environmentVariableName, bool fromEnvironment)
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
            throw new InvalidOperationException($"未配置数据库连接字符串，请设置环境变量 {_environmentVariableName}");
        }

        return new OracleConnection(connectionString);
    }
}
