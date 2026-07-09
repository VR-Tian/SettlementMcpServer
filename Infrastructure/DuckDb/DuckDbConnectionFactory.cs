using System.Data;
using DuckDB.NET.Data;
using SettlementMcpServer.Contracts;

namespace SettlementMcpServer.Infrastructure.DuckDb;

/// <summary>
/// DuckDB 数据库连接工厂
/// </summary>
/// <remarks>
/// <para>
/// 负责创建 DuckDB 数据库连接。DuckDB 使用嵌入式模式，数据保存在本地文件中。
/// </para>
/// <para>
/// 数据库文件保存在 <c>%TEMP%\SettlementMcpServer\duckdb\settlement.db</c>，
/// 首次使用时自动创建目录和数据库文件。
/// </para>
/// <para>
/// <b>共享连接：</b>
/// DuckDB 是嵌入式数据库，同一时间只允许一个进程以读写模式打开数据库文件。
/// 因此本工厂使用单例共享连接，所有仓储共用同一个连接实例，避免文件锁定冲突。
/// </para>
/// </remarks>
public sealed class DuckDbConnectionFactory : IDbConnectionFactory, IDisposable
{
    /// <summary>
    /// DuckDB 数据库文件路径
    /// </summary>
    private readonly string _databasePath;

    /// <summary>
    /// 共享的 DuckDB 连接实例
    /// </summary>
    private DuckDBConnection? _sharedConnection;

    /// <summary>
    /// 锁对象，用于线程安全地创建共享连接
    /// </summary>
    private readonly object _lock = new();

    /// <summary>
    /// 初始化 DuckDB 连接工厂
    /// </summary>
    public DuckDbConnectionFactory()
    {
        // 数据库文件保存在 %TEMP%\SettlementMcpServer\duckdb\
        var baseDirectory = Path.Combine(Path.GetTempPath(), "SettlementMcpServer", "duckdb");
        Directory.CreateDirectory(baseDirectory);

        _databasePath = Path.Combine(baseDirectory, "settlement.db");
    }

    /// <summary>
    /// 获取共享的 DuckDB 数据库连接
    /// </summary>
    /// <returns>已打开的 DuckDB 数据库连接实例（所有调用方共享）</returns>
    /// <remarks>
    /// <para>
    /// DuckDB 是嵌入式数据库，同一时间只允许一个进程以读写模式打开数据库文件。
    /// 因此本方法返回共享连接，而非每次创建新连接。
    /// </para>
    /// <para>
    /// 调用方<b>不应</b>使用 <c>using</c> 语句释放此连接，因为它是全局共享的。
    /// 连接会在应用程序退出时自动释放。
    /// </para>
    /// </remarks>
    public IDbConnection CreateConnection()
    {
        if (_sharedConnection == null || _sharedConnection.State != ConnectionState.Open)
        {
            lock (_lock)
            {
                if (_sharedConnection == null || _sharedConnection.State != ConnectionState.Open)
                {
                    var connection = new DuckDBConnection($"Data Source={_databasePath}");
                    connection.Open();
                    _sharedConnection = connection;
                }
            }
        }

        return _sharedConnection;
    }

    /// <summary>
    /// 释放共享连接资源
    /// </summary>
    public void Dispose()
    {
        lock (_lock)
        {
            if (_sharedConnection != null)
            {
                if (_sharedConnection.State == ConnectionState.Open)
                {
                    _sharedConnection.Close();
                }
                _sharedConnection.Dispose();
                _sharedConnection = null;
            }
        }
    }
}
