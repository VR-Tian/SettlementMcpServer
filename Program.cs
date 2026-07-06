using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;
using SettlementMcpServer.Extensions;
using SettlementMcpServer.Tools;

var builder = Host.CreateApplicationBuilder(args);

// 日志配置：使用 Serilog 输出到文件
// MCP 协议使用 stdout 进行通信，因此日志输出到文件避免污染协议消息
var logDirectory = Path.Combine(Path.GetTempPath(), "SettlementMcpServer", "logs");
Directory.CreateDirectory(logDirectory);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.File(
        path: Path.Combine(logDirectory, "log-.txt"),
        rollingInterval: RollingInterval.Day,
        fileSizeLimitBytes: 10 * 1024 * 1024, // 10MB
        retainedFileCountLimit: 7, // 保留最近 7 天
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Logging.ClearProviders();
builder.Logging.AddSerilog(Log.Logger);

// ========================================
// 服务注册
// ========================================

// 注册 MCP Server 核心组件
// WithStdioServerTransport：使用标准输入/输出作为 MCP 协议传输通道
// WithTools<T>：注册 MCP 工具类，框架会自动从 DI 容器解析工具类的依赖
builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    //.WithTools<RandomNumberTools>()
    .WithTools<AuditServerTools>()
    .WithTools<SyncDataToDuckDbTools>()
    .WithTools<AnalysisDimensionTools>()
    .WithTools<DuckDbQueryTools>();
    //.WithTools<YuehaiSettlementTools>();

// 注册审核数据访问服务（连接字符串通过环境变量 ORACLE_CONNECTION_STRING 延迟读取）
builder.Services.AddOracleDataAccess("ORACLE_CONNECTION_STRING");

// 注册YueHai医保结算数据访问服务（连接字符串通过环境变量 YUEHAI_SETTLEMENT_ORACLE_CONNECTION_STRING 延迟读取）
builder.Services.AddYuehaiSettlementDataAccess("YUEHAI_SETTLEMENT_ORACLE_CONNECTION_STRING");

// 注册 Excel 导出服务
// 此扩展方法内部注册了 IExcelExportService → MiniExcelExportService 映射
builder.Services.AddExcelExport();

// 注册 DuckDB 相关服务（连接工厂、数据同步、查询服务、分析维度）
builder.Services.AddDuckDbServices();

// 注册重复收费规则引擎服务（规则加载器、四种规则执行器、规则管道）
builder.Services.AddRuleEngine();

// ========================================
// 启动运行
// ========================================
// Build() 构建完整的 DI 容器和 Host
var app = builder.Build();

// 初始化规则数据（从 Excel 扫描并写入 DuckDB）
try
{
    using var scope = app.Services.CreateScope();
    var initService = scope.ServiceProvider.GetRequiredService<SettlementMcpServer.Contracts.IRuleInitializationService>();
    await initService.InitializeRulesAsync();
}
catch (Exception ex)
{
    Log.Fatal(ex, "规则初始化失败，应用程序将退出");
    await app.StopAsync();
    return;
}

// RunAsync() 启动 Host 并开始处理 MCP 请求（阻塞直到应用停止）
await app.RunAsync();
Log.CloseAndFlush();
