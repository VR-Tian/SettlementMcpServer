using System.Data;
using System.Data.Common;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SettlementMcpServer.Contracts;
using SettlementMcpServer.Models.Rules;

namespace SettlementMcpServer.Infrastructure.DuckDb;

/// <summary>
/// 基于 DuckDB 的规则仓储实现
/// </summary>
/// <remarks>
/// <para>
/// 使用 DuckDB 嵌入式数据库存储规则数据。
/// 规则集（<see cref="IRuleSet"/>）通过 <see cref="System.Text.Json"/> 序列化后存储在 <c>rule_json</c> 字段中，
/// 同时将规则类别存储在 <c>rule_category</c> 字段中以便区分。
/// </para>
/// </remarks>
public sealed class DuckDbRuleRepository : IRuleRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<DuckDbRuleRepository> _logger;

    /// <summary>
    /// JSON 序列化选项
    /// </summary>
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    /// <summary>
    /// 初始化 DuckDB 规则仓储
    /// </summary>
    /// <param name="connectionFactory">DuckDB 连接工厂（通过 Keyed Services 注入）</param>
    /// <param name="logger">日志记录器</param>
    public DuckDbRuleRepository(
        [FromKeyedServices("duckdb")] IDbConnectionFactory connectionFactory,
        ILogger<DuckDbRuleRepository>? logger = null)
    {
        _connectionFactory = connectionFactory ?? throw new ArgumentNullException(nameof(connectionFactory));
        _logger = logger ?? NullLogger<DuckDbRuleRepository>.Instance;
    }

    /// <inheritdoc />
    public async Task<IRuleSet?> GetRuleSetByCodeAsync(string ruleCode, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(ruleCode);

        _logger.LogDebug("从数据库加载规则，规则编码: {RuleCode}", ruleCode);

        await EnsureTableExistsAsync(cancellationToken);

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        var dbCommand = (DbCommand)connection.CreateCommand();
        dbCommand.CommandText = "SELECT rule_category, rule_json FROM audit_rules WHERE rule_code = @rule_code LIMIT 1";
        var param = dbCommand.CreateParameter();
        param.ParameterName = "rule_code";
        param.Value = ruleCode;
        dbCommand.Parameters.Add(param);

        var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);
        if (await reader.ReadAsync(cancellationToken))
        {
            var category = reader.GetInt32(0);
            var ruleJson = reader.GetString(1);
            
            var ruleSet = DeserializeRuleSet((RuleCategory)category, ruleJson);
            _logger.LogDebug("规则 {RuleCode} 加载成功，类别: {Category}", ruleCode, (RuleCategory)category);
            return ruleSet;
        }

        _logger.LogDebug("规则 {RuleCode} 在数据库中不存在", ruleCode);
        return null;
    }

    /// <inheritdoc />
    public async Task SaveRuleSetAsync(IRuleSet ruleSet, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);

        await EnsureTableExistsAsync(cancellationToken);

        var ruleCode = ruleSet.RuleCode;
        var category = (int)ruleSet.Category;

        _logger.LogInformation("保存规则 {RuleCode} 到数据库，类别: {Category}", ruleCode, ruleSet.Category);

        // 序列化完整的规则集为 JSON
        var ruleJson = JsonSerializer.Serialize(ruleSet, ruleSet.GetType(), JsonOptions);

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        // 先尝试更新，如果不存在则插入
        var dbCommand = (DbCommand)connection.CreateCommand();
        dbCommand.CommandText = """
            UPDATE audit_rules SET
                rule_category = @rule_category,
                rule_json = @rule_json,
                updated_at = CURRENT_TIMESTAMP
            WHERE rule_code = @rule_code
            """;

        AddParameter(dbCommand, "rule_code", ruleCode);
        AddParameter(dbCommand, "rule_category", category);
        AddParameter(dbCommand, "rule_json", ruleJson);

        var rowsAffected = await dbCommand.ExecuteNonQueryAsync(cancellationToken);

        if (rowsAffected == 0)
        {
            // 记录不存在，执行插入
            var insertCommand = (DbCommand)connection.CreateCommand();
            insertCommand.CommandText = """
                INSERT INTO audit_rules (
                    rule_code, rule_category, rule_json, created_at, updated_at
                ) VALUES (
                    @rule_code, @rule_category, @rule_json, CURRENT_TIMESTAMP, CURRENT_TIMESTAMP
                )
                """;

            AddParameter(insertCommand, "rule_code", ruleCode);
            AddParameter(insertCommand, "rule_category", category);
            AddParameter(insertCommand, "rule_json", ruleJson);

            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            _logger.LogInformation("规则 {RuleCode} 插入成功", ruleCode);
        }
        else
        {
            _logger.LogInformation("规则 {RuleCode} 更新成功", ruleCode);
        }
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetAllRuleCodesAsync(CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync(cancellationToken);

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        var dbCommand = (DbCommand)connection.CreateCommand();
        dbCommand.CommandText = "SELECT rule_code FROM audit_rules ORDER BY rule_code";

        var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);
        var ruleCodes = new List<string>();

        while (await reader.ReadAsync(cancellationToken))
        {
            ruleCodes.Add(reader.GetString(0));
        }

        _logger.LogDebug("获取到 {Count} 个规则编码", ruleCodes.Count);
        return ruleCodes.AsReadOnly();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> GetRuleCodesByCategoryAsync(RuleCategory category, CancellationToken cancellationToken = default)
    {
        await EnsureTableExistsAsync(cancellationToken);

        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        var dbCommand = (DbCommand)connection.CreateCommand();
        dbCommand.CommandText = "SELECT rule_code FROM audit_rules WHERE rule_category = @rule_category ORDER BY rule_code";
        AddParameter(dbCommand, "rule_category", (int)category);

        var reader = await dbCommand.ExecuteReaderAsync(cancellationToken);
        var ruleCodes = new List<string>();

        while (await reader.ReadAsync(cancellationToken))
        {
            ruleCodes.Add(reader.GetString(0));
        }

        _logger.LogDebug("获取到 {Count} 个 {Category} 类别的规则编码", ruleCodes.Count, category);
        return ruleCodes.AsReadOnly();
    }

    /// <summary>
    /// 根据规则类别反序列化规则集
    /// </summary>
    /// <param name="category">规则类别</param>
    /// <param name="ruleJson">JSON 字符串</param>
    /// <returns>反序列化后的规则集</returns>
    private static IRuleSet? DeserializeRuleSet(RuleCategory category, string ruleJson)
    {
        return category switch
        {
            RuleCategory.DuplicateCharge => JsonSerializer.Deserialize<DuplicateChargeRuleSet>(ruleJson, JsonOptions),
            RuleCategory.FrequencyLimit => JsonSerializer.Deserialize<FrequencyLimitRuleSet>(ruleJson, JsonOptions),
            _ => throw new InvalidOperationException($"不支持的规则类别: {category}")
        };
    }

    /// <summary>
    /// 确保规则表存在（如果不存在则创建）
    /// </summary>
    /// <param name="cancellationToken">取消令牌</param>
    private async Task EnsureTableExistsAsync(CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        connection.Open();

        var dbCommand = (DbCommand)connection.CreateCommand();
        dbCommand.CommandText = """
            CREATE TABLE IF NOT EXISTS audit_rules (
                rule_code VARCHAR PRIMARY KEY,
                rule_category INTEGER NOT NULL,
                rule_json VARCHAR NOT NULL,
                created_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
                updated_at TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP
            )
            """;

        await dbCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    /// <summary>
    /// 添加参数到 DbCommand
    /// </summary>
    /// <param name="command">数据库命令</param>
    /// <param name="parameterName">参数名称</param>
    /// <param name="value">参数值</param>
    private static void AddParameter(DbCommand command, string parameterName, object? value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = parameterName;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
