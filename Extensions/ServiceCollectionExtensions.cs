using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Infrastructure;
using SettlementMcpServer.Infrastructure.Analysis;
using SettlementMcpServer.Infrastructure.DataSync;
using SettlementMcpServer.Infrastructure.DuckDb;
using SettlementMcpServer.Infrastructure.Rules;
using SettlementMcpServer.Infrastructure.Rules.Executors;

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
    /// 注册医保结算数据访问服务（通过环境变量名动态读取连接字符串）
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="connectionStringEnvName">连接字符串对应的环境变量名</param>
    public static IServiceCollection AddSettlementDataAccess(this IServiceCollection services, string connectionStringEnvName)
    {
        SettlementTypeMap.Register();
        services.AddKeyedSingleton<IDbConnectionFactory>("",
            (_, _) => new OracleDbConnectionFactory(connectionStringEnvName, fromEnvironment: true));
        services.AddSingleton<ISettlementDataRepository, OracleSettlementDataRepository>();
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
    /// 注册 DuckDB 相关服务（连接工厂、数据同步、查询服务、分析维度、审核结果仓储）
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

        // 注册审核结果仓储
        services.AddSingleton<IAuditResultRepository, DuckDbAuditResultRepository>();

        // 注册审核任务仓储
        services.AddSingleton<IAuditTaskRepository, DuckDbAuditTaskRepository>();

        // 注册 DuckDB 结算数据仓储（用于规则执行时从 DuckDB 查询结算数据）
        services.AddSingleton<DuckDbSettlementDataRepository>();

        return services;
    }

    /// <summary>
    /// 注册规则引擎服务
    /// </summary>
    /// <remarks>
    /// <para>
    /// 注册规则引擎相关服务，包括：
    /// </para>
    /// <list type="bullet">
    ///   <item><description><see cref="IRuleRepository"/>：规则仓储（DuckDB 实现）</description></item>
    ///   <item><description><see cref="IRuleLoader"/>：规则加载器（支持多种规则类别）</description></item>
    ///   <item><description><see cref="IRuleInitializationService"/>：规则初始化服务</description></item>
    ///   <item><description><see cref="IRuleExecutor"/>：规则执行器（支持多种规则类别）</description></item>
    ///   <item><description><see cref="IRulePipeline"/>：规则管道编排</description></item>
    /// </list>
    /// </remarks>
    public static IServiceCollection AddRuleEngine(this IServiceCollection services)
    {
        // 注册规则仓储（DuckDB 实现）
        services.AddSingleton<IRuleRepository, DuckDbRuleRepository>();

        // 注册重复收费规则 Excel 加载器（作为具体类型，用于初始化阶段从 Excel 加载规则）
        services.AddSingleton<DuplicateChargeExcelRuleLoader>();

        // 注册限定频次规则 Excel 加载器（作为具体类型，用于初始化阶段从 Excel 加载规则）
        services.AddSingleton<FrequencyLimitExcelRuleLoader>();

        // 注册规则加载器集合（通过工厂模式，根据规则类别选择对应的加载器）
        services.AddSingleton<IRuleLoader>(sp =>
        {
            var duplicateChargeLoader = sp.GetRequiredService<DuplicateChargeExcelRuleLoader>();
            var frequencyLimitLoader = sp.GetRequiredService<FrequencyLimitExcelRuleLoader>();

            // 返回一个复合加载器，根据规则类别自动选择
            return new CompositeRuleLoader(new IRuleLoader[] { duplicateChargeLoader, frequencyLimitLoader });
        });

        // 注册规则初始化服务
        services.AddSingleton<IRuleInitializationService, RuleInitializationService>();

        // 注册重复收费规则执行器
        services.AddSingleton<IRuleExecutor, DuplicateChargeExecutor>();

        // 注册限定频次规则执行器
        services.AddSingleton<IRuleExecutor, FrequencyLimitExecutor>();

        // 注册规则管道
        services.AddSingleton<IRulePipeline, RulePipeline>();

        // 注册规则组合执行器
        services.AddSingleton<IRuleCombinationExecutor, RuleCombinationExecutor>();

        // 注册审核任务处理器（Transient 生命周期，每次请求创建新实例）
        services.AddTransient<AuditTaskProcessor>();

        return services;
    }

    public static IServiceCollection AddJsonSerializerOptions(this IServiceCollection services)
    {
        services.AddSingleton(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            ReferenceHandler = ReferenceHandler.Preserve,
            MaxDepth = 256,
            AllowTrailingCommas = false, // MCP标准不允许尾逗号
            NumberHandling = JsonNumberHandling.Strict
        });
        return services;
    }
}
