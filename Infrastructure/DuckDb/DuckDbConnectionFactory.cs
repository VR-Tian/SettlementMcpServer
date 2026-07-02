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
/// </remarks>
public sealed class DuckDbConnectionFactory : IDbConnectionFactory
{
    /// <summary>
    /// DuckDB 数据库文件路径
    /// </summary>
    private readonly string _databasePath;

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
    /// 创建 DuckDB 数据库连接
    /// </summary>
    /// <returns>未打开的 DuckDB 数据库连接实例</returns>
    /// <remarks>
    /// <para>
    /// 连接字符串使用数据库文件路径，DuckDB 会自动创建数据库文件（如果不存在）。
    /// </para>
    /// <para>
    /// 调用方必须通过 <c>using</c> 语句确保连接被正确释放。
    /// </para>
    /// </remarks>
    public IDbConnection CreateConnection()
    {
        return new DuckDBConnection($"Data Source={_databasePath}");
    }
}
