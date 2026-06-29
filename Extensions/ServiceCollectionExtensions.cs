using Microsoft.Extensions.DependencyInjection;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Infrastructure;

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
        services.AddSingleton<IDbConnectionFactory>(_ => new OracleDbConnectionFactory(connectionStringEnvName, fromEnvironment: true));
        services.AddSingleton<IAuditDataRepository, OracleAuditDataRepository>();
        return services;
    }

    /// <summary>
    /// 注册粤海医保结算数据访问服务（通过环境变量名动态读取连接字符串）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionStringEnvName">连接字符串对应的环境变量名</param>
    public static IServiceCollection AddYuehaiSettlementDataAccess(this IServiceCollection services, string connectionStringEnvName)
    {
        YuehaiSettlementTypeMap.Register();
        services.AddSingleton<IYuehaiSettlementConnectionFactory>(_ => new OracleDbConnectionFactory(connectionStringEnvName, fromEnvironment: true));
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
}
