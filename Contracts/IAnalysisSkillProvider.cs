using SettlementMcpServer.Models;

namespace SettlementMcpServer.Contracts;

/// <summary>
/// 分析维度提供者接口
/// </summary>
/// <remarks>
/// <para>
/// 提供预定义的分析维度列表，供 LLM 选择合适的分析视角。
/// 每个维度包含名称、描述和对应的 SQL 查询模板。
/// </para>
/// </remarks>
public interface IAnalysisSkillProvider
{
    /// <summary>
    /// 获取所有可用的分析维度
    /// </summary>
    /// <returns>分析维度列表</returns>
    IReadOnlyList<AnalysisDimension> GetAllDimensions();

    /// <summary>
    /// 根据名称获取指定的分析维度
    /// </summary>
    /// <param name="name">维度名称</param>
    /// <returns>分析维度，如果不存在则返回 null</returns>
    AnalysisDimension? GetDimensionByName(string name);
}
