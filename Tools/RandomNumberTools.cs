using System.ComponentModel;
using ModelContextProtocol.Server;

namespace SettlementMcpServer.Tools;

/// <summary>
/// 随机数生成工具
/// </summary>
/// <remarks>
/// 此类演示了不依赖外部服务的简单 MCP 工具实现。
/// 与 <see cref="AuditServerTools"/> 对比，此类无构造函数参数，说明并非所有工具都需要 DI。
/// </remarks>
internal class RandomNumberTools
{
    /// <summary>
    /// 生成指定范围内的随机整数
    /// </summary>
    /// <param name="min">最小值（包含 ）</param>
    /// <param name="max">最大值（不包含）</param>
    /// <returns>随机整数</returns>
    /// <remarks>
    /// 使用 <see cref="Random.Shared"/> 避免每次调用都创建新的 Random 实例。
    /// <see cref="Random.Shared"/> 是线程安全的单例随机数生成器。
    /// </remarks>
    [McpServerTool]
    [Description("Generates a random number between the specified minimum and maximum values.（返回指定范围内的随机整数）")]
    public int GetRandomNumber(
        [Description("Minimum value (inclusive)")] int min = 0,
        [Description("Maximum value (exclusive)")] int max = 100)
    {
        return Random.Shared.Next(min, max);
    }
}
