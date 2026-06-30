using Dapper;

namespace SettlementMcpServer.Infrastructure;

/// <summary>
/// SQL WHERE 条件构建辅助类，消除仓储层重复的 WHERE 条件构建逻辑
/// </summary>
public static class SqlWhereBuilder
{
    /// <summary>
    /// 向 WHERE 条件列表和参数集合添加一个可选过滤条件
    /// </summary>
    /// <param name="value">过滤值，为 null 或空白时忽略此条件</param>
    /// <param name="columnName">数据库表中的列名</param>
    /// <param name="paramName">SQL 参数名（用于 :paramName 占位符）</param>
    /// <param name="conditions">WHERE 条件集合，方法会向其中添加条件表达式</param>
    /// <param name="parameters">Dapper 动态参数集合，方法会向其中添加参数</param>
    public static void AddCondition(
        string? value,
        string columnName,
        string paramName,
        List<string> conditions,
        DynamicParameters parameters)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            conditions.Add($"{columnName} = :{paramName}");
            parameters.Add(paramName, value);
        }
    }

    /// <summary>
    /// 将条件列表转换为完整的 WHERE 子句
    /// </summary>
    /// <param name="conditions">WHERE 条件集合</param>
    /// <returns>完整的 WHERE 子句，若无条件则返回空字符串</returns>
    public static string BuildWhereClause(List<string> conditions)
    {
        return conditions.Count > 0 ? "WHERE " + string.Join(" AND ", conditions) : string.Empty;
    }
}
