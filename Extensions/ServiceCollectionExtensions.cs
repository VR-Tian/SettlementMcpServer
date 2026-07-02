using Microsoft.Extensions.DependencyInjection;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Infrastructure;
using SettlementMcpServer.Infrastructure.Analysis;
using SettlementMcpServer.Infrastructure.DataSync;
using SettlementMcpServer.Infrastructure.DuckDb;

namespace SettlementMcpServer.Extensions;

/// <summary>
/// 服务注册扩展
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册 Oracle 审核数据访问服务（通过环境变量名动态读取连接字符串）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionStringEnvName">连接字符串对应的环境变量名</param>
    public static IServiceCollection AddOracleDataAccess(this IServiceCollection services, string connectionStringEnvName)
    {
        AuditedResultTypeMap.Register();
        services.AddKeyedSingleton<IDbConnectionFactory>("audit",
            (_, _) => new OracleDbConnectionFactory(connectionStringEnvName, fromEnvironment: true));
        services.AddSingleton<IAuditDataRepository, OracleAuditDataRepository>();
        return services;
    }

    /// <summary>
    /// 注册YueHai医保结算数据访问服务（通过环境变量名动态读取连接字符串）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionStringEnvName">连接字符串对应的环境变量名</param>
    public static IServiceCollection AddYuehaiSettlementDataAccess(this IServiceCollection services, string connectionStringEnvName)
    {
        YuehaiSettlementTypeMap.Register();
        services.AddKeyedSingleton<IDbConnectionFactory>("yuehai",
            (_, _) => new OracleDbConnectionFactory(connectionStringEnvName, fromEnvironment: true));
        services.AddSingleton<IYuehaiSettlementDataRepository, OracleYuehaiSettlementDataRepository>();
        return services;
    }

    /// <summary>
    /// 注册 Excel 导出服务
    /// </summary>
    public static IServiceCollection AddExcelExport(this IServiceCollection services)
    {
        services.AddSingleton<IExcelExportService, MiniExcelExportService>();
        return services;
    }

    /// <summary>
    /// 注册 DuckDB 相关服务（连接工厂、数据同步、查询服务、分析维度）
    /// </summary>
    public static IServiceCollection AddDuckDbServices(this IServiceCollection services)
    {
        // 注册 DuckDB 连接工厂（使用 "duckdb" 键名）
        services.AddKeyedSingleton<IDbConnectionFactory>("duckdb",
            (_, _) => new DuckDbConnectionFactory());

        // 注册数据同步服务
        services.AddSingleton<IDataSyncService, ParquetDataSyncService>();

        // 注册 DuckDB 查询服务
        services.AddSingleton<IDuckDbQueryService, DuckDbQueryService>();

        // 注册分析维度提供者
        services.AddSingleton<IAnalysisSkillProvider, AnalysisSkillProvider>();

        return services;
    }
}
