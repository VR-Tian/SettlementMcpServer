# 医保结算审核 MCP Server

基于 .NET 10 构建的医保结算数据审核 MCP 服务器，提供规则引擎审核、数据同步、多维度分析等功能。

## 功能特性

### 1. 规则引擎审核
- **重复收费规则**：支持 4 种规则类型
  - 跨组共存（同一人同一天 A 组和 B 组项目同时收取）
  - 组内重复低价（同 A 组内重复收费，提示低价项目）
  - 阈值后存在（A 组数量超阈值后检查 B 组项目）
  - 跨组数量阈值（B 组数量超阈值触发违规）
- **限定频次规则**：审核项目收费频次是否超出限定次数
- **规则管道**：支持多规则组合审核，自动生成违规报告

### 2. 数据同步与存储
- 从 Oracle 数据库同步医保结算数据到 DuckDB
- 使用 Parquet 格式存储，支持高性能列式查询
- 自动创建索引和优化查询性能

### 3. 多维度分析
- 提供预定义分析维度（按机构、科室、时间、项目等）
- 支持自定义 SQL 查询（DuckDB 语法）
- 分析结果可导出为 Excel 文件

### 4. 审核结果管理
- 分页查询审核结果
- 批量导出 Excel 报告
- 支持按规则类别、违规类型筛选

## 技术架构

### 核心组件
- **MCP Server**：基于 ModelContextProtocol C# SDK，使用 stdio 传输
- **数据库**：Oracle（源数据）+ DuckDB（分析存储）
- **规则引擎**：管道-过滤器架构，策略模式实现规则执行器
- **日志系统**：Serilog 文件日志，按日滚动，保留 7 天

### 项目结构
```
SettlementMcpServer/
├── Contracts/          # 接口定义层
├── Models/             # 领域模型层
│   └── Rules/          # 规则相关模型
├── Infrastructure/     # 基础设施层
│   ├── Rules/          # 规则引擎实现
│   │   └── Executors/  # 规则执行器
│   ├── DuckDb/         # DuckDB 数据访问
│   └── Oracle*         # Oracle 数据访问
├── Tools/              # MCP 工具类
└── Extensions/         # 服务注册扩展
```

## 环境要求

- .NET 10 SDK 或更高版本
- Oracle 数据库连接（用于读取医保结算数据）
- 操作系统：Windows x64/ARM64、Linux x64/ARM64、macOS ARM64

## 配置说明

### 环境变量
```bash
# Oracle 数据库连接字符串（审核数据源）
ORACLE_CONNECTION_STRING="Data Source=...;User Id=...;Password=..."

# Oracle 数据库连接字符串（医保结算数据源）
YUEHAI_SETTLEMENT_ORACLE_CONNECTION_STRING="Data Source=...;User Id=...;Password=..."
```

### 规则文件
规则 Excel 文件存放在 `.agents/skills/` 目录下：
- `重复收费规则/130301规则内涵.xlsx`
- `限定频次/限定频次120501.xlsx`

应用启动时自动扫描并加载规则到 DuckDB。

## 本地开发

### 1. 克隆项目
```bash
git clone <repository-url>
cd SettlementMcpServer
```

### 2. 配置环境变量
设置 Oracle 数据库连接字符串（见上文配置说明）。

### 3. 运行项目
```bash
dotnet run
```

### 4. 配置 MCP 客户端
在 IDE 中配置 MCP 服务器连接：

**VS Code** (`.vscode/mcp.json`):
```json
{
  "servers": {
    "SettlementMcpServer": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "c:/Users/dp/Desktop/work/code/SettlementMcpServer"
      ]
    }
  }
}
```

**Visual Studio** (`.mcp.json`):
```json
{
  "servers": {
    "SettlementMcpServer": {
      "type": "stdio",
      "command": "dotnet",
      "args": [
        "run",
        "--project",
        "c:\\Users\\dp\\Desktop\\work\\code\\SettlementMcpServer"
      ]
    }
  }
}
```

## MCP 工具列表

### AuditServerTools（审核服务）
- `exec_audit_analysis`：执行规则审核分析
- `get_audited_result_count`：获取审核结果总数
- `query_audited_results`：分页查询审核结果
- `export_audited_results_to_excel`：导出审核结果为 Excel

### SyncDataToDuckDbTools（数据同步）
- `sync_yuehai_settlement_data`：同步医保结算数据到 DuckDB
- `sync_audited_result_data`：同步审核结果数据到 DuckDB

### AnalysisDimensionTools（分析维度）
- `get_available_analysis_dimensions`：获取可用分析维度列表
- `get_analysis_sql`：获取指定维度的 SQL 查询模板

### DuckDbQueryTools（查询执行）
- `execute_duck_db_query`：执行 DuckDB SQL 查询

## 发布 NuGet 包

1. 更新 `.csproj` 中的包元数据：
   ```xml
   <PackageId>SettlementMcpServer</PackageId>
   <Version>1.0.0</Version>
   <Authors>YourName</Authors>
   <Description>医保结算审核 MCP 服务器</Description>
   ```

2. 打包项目：
   ```bash
   dotnet pack -c Release
   ```

3. 发布到 NuGet.org：
   ```bash
   dotnet nuget push bin/Release/*.nupkg --api-key <your-api-key> --source https://api.nuget.org/v3/index.json
   ```

## 使用 NuGet 包

从 NuGet.org 安装后，在 IDE 中配置：

```json
{
  "servers": {
    "SettlementMcpServer": {
      "type": "stdio",
      "command": "dnx",
      "args": [
        "SettlementMcpServer",
        "--version",
        "1.0.0",
        "--yes"
      ]
    }
  }
}
```

## 日志位置

日志文件存储在系统临时目录：
- Windows: `%TEMP%\SettlementMcpServer\logs\`
- Linux/macOS: `/tmp/SettlementMcpServer/logs/`

日志按日滚动，每个文件最大 10MB，保留最近 7 天。

## 相关文档

- [MCP 协议规范](https://spec.modelcontextprotocol.io/)
- [MCP C# SDK](https://github.com/modelcontextprotocol/csharp-sdk)
- [DuckDB 文档](https://duckdb.org/docs/)
- [MiniExcel 文档](https://github.com/mini-software/MiniExcel)

## 许可证

本项目采用私有许可证，仅供内部使用。
