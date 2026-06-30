using Dapper;
using System.Reflection;

namespace SettlementMcpServer.Infrastructure;

/// <summary>
/// Dapper 自定义类型映射器基类 - 将数据库中文列名映射到模型英文属性名
/// </summary>
/// <remarks>
/// <para>
/// 本基类封装了 <see cref="SqlMapper.ITypeMap"/> 的通用实现，子类只需通过
/// <see cref="ColumnMappings"/> 属性提供"列名 → 属性名"的映射字典即可，
/// 无需重复编写反射、构造函数查找、成员映射等样板代码。
/// </para>
/// <para>
/// <b>为什么需要自定义类型映射？</b>
/// </para>
/// <para>
/// Dapper 默认通过列名和属性名的字符串匹配（忽略大小写）完成数据映射。
/// 但本项目的 Oracle 表使用中文列名，而 C# 模型类使用英文属性名，两者无法直接匹配。
/// 通过实现 <see cref="SqlMapper.ITypeMap"/> 接口，可以在不修改 SQL（保持 SELECT *）
/// 的前提下，通过字典维护列名到属性名的映射关系。
/// </para>
/// <para>
/// <b>注册时机：</b>
/// 类型映射必须在 Dapper 首次查询目标类型之前注册。
/// 推荐在 DI 注册阶段调用子类的 <c>Register()</c> 静态方法，确保映射在应用启动时完成。
/// </para>
/// </remarks>
internal abstract class DapperTypeMapBase : SqlMapper.ITypeMap
{
    /// <summary>
    /// 列名 → 属性名映射字典（由子类提供）
    /// </summary>
    /// <remarks>
    /// <para>
    /// 键为数据库中的原始列名（如中文列名），值为目标模型类中的对应属性名。
    /// 建议使用 <see cref="StringComparer.OrdinalIgnoreCase"/> 实现大小写不敏感的键匹配。
    /// </para>
    /// <para>
    /// 新增字段时只需在此字典中添加一行映射，无需修改 SQL 或其他代码。
    /// </para>
    /// </remarks>
    protected abstract Dictionary<string, string> ColumnMappings { get; }

    private readonly Type _type;
    private readonly ConstructorInfo? _constructor;
    private readonly PropertyInfo[] _properties;

    /// <summary>
    /// 初始化类型映射器实例
    /// </summary>
    /// <param name="type">目标模型类型</param>
    /// <remarks>
    /// 通过反射获取目标类型的所有公共实例属性和构造函数信息，
    /// 供 Dapper 在映射时查询。
    /// </remarks>
    protected DapperTypeMapBase(Type type)
    {
        _type = type;
        // 获取所有公共实例属性
        _properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

        // 选择参数最多的构造函数（用于构造函数注入场景，当前模型类无参构造函数优先）
        _constructor = type.GetConstructors()
            .OrderByDescending(c => c.GetParameters().Length)
            .FirstOrDefault();
    }

    /// <summary>
    /// 返回首选构造函数
    /// </summary>
    /// <remarks>
    /// Dapper 支持通过构造函数注入属性值（而非先调用无参构造再设置属性）。
    /// 本模型类使用无参构造 + 属性设置模式，此属性返回 null 即可让 Dapper 走默认路径。
    /// </remarks>
    public ConstructorInfo? Constructor => _constructor;

    /// <summary>
    /// 根据查询结果集的列名和类型列表，查找合适的构造函数
    /// </summary>
    /// <param name="names">查询结果集中的列名数组</param>
    /// <param name="types">查询结果集中对应列的 CLR 类型数组</param>
    /// <returns>匹配的构造函数，无匹配时返回 null</returns>
    public ConstructorInfo? FindConstructor(string[] names, Type[] types)
    {
        return _constructor;
    }

    /// <summary>
    /// 查找显式标记的构造函数（用于 [DapperConstructor] 特性标注场景）
    /// </summary>
    public ConstructorInfo? FindExplicitConstructor()
    {
        return _constructor;
    }

    /// <summary>
    /// 获取构造函数参数与数据库列的映射关系
    /// </summary>
    /// <param name="constructor">目标构造函数</param>
    /// <param name="columnName">数据库列名（原始列名）</param>
    /// <returns>成员映射信息</returns>
    /// <remarks>
    /// 当使用构造函数注入时，Dapper 调用此方法确定哪个参数对应哪个列。
    /// 本实现通过映射字典将原始列名转换为英文参数名进行匹配。
    /// </remarks>
    public SqlMapper.IMemberMap GetConstructorParameter(ConstructorInfo constructor, string columnName)
    {
        // 1. 通过映射字典获取对应的英文属性/参数名
        var propertyName = ColumnMappings.TryGetValue(columnName, out var mappedName)
            ? mappedName
            : columnName;

        // 2. 在构造函数参数中查找同名参数
        var match = constructor.GetParameters()
            .FirstOrDefault(p => string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

        // 3. 找到则返回映射，否则返回 null（Dapper 会忽略该列）
        return match != null ? new SimpleMemberMap(columnName, match) : null!;
    }

    /// <summary>
    /// 获取数据库列与类属性的映射关系（核心方法）
    /// </summary>
    /// <param name="columnName">数据库列名（原始列名）</param>
    /// <returns>成员映射信息，未找到映射时返回 null</returns>
    /// <remarks>
    /// <para>
    /// 这是 Dapper 映射过程中最关键的方法。Dapper 每读取一行数据时，
    /// 会对结果集中的每一列调用此方法，获取对应的属性 PropertyInfo。
    /// </para>
    /// <para>
    /// 映射流程：
    /// <list type="number">
    ///   <item>通过 <see cref="ColumnMappings"/> 字典将原始列名转为英文属性名</item>
    ///   <item>在类型属性列表中查找匹配的属性</item>
    ///   <item>返回 <see cref="SimpleMemberMap"/> 包装映射关系</item>
    ///   <item>如果未找到映射，返回 null（Dapper 内部会跳过该列）</item>
    /// </list>
    /// </para>
    /// </remarks>
    public SqlMapper.IMemberMap GetMember(string columnName)
    {
        // 1. 将原始列名映射为英文属性名
        var propertyName = ColumnMappings.TryGetValue(columnName, out var mappedName)
            ? mappedName
            : columnName;

        // 2. 在类型属性列表中查找匹配的属性
        var property = _properties.FirstOrDefault(p =>
            string.Equals(p.Name, propertyName, StringComparison.OrdinalIgnoreCase));

        // 3. 找到则返回属性映射，否则返回 null（Dapper 会跳过该列）
        // Dapper 内部通过判断返回值是否为 null 来决定是否忽略该列，
        // 返回 null 是最安全的做法，不会触发任何异常。
        return property != null ? new SimpleMemberMap(columnName, property) : null!;
    }
}

/// <summary>
/// 简单的成员映射实现 - 包装单个 PropertyInfo 或 ParameterInfo
/// </summary>
/// <remarks>
/// 此类用于告诉 Dapper "这个数据库列应该映射到对象的这个属性/参数"。
/// Dapper 通过反射获取 <see cref="Property"/> 或 <see cref="Parameter"/> 后，
/// 使用 IL 发射技术生成高效赋值代码。
/// </remarks>
internal sealed class SimpleMemberMap : SqlMapper.IMemberMap
{
    private readonly string _columnName;
    private readonly PropertyInfo? _property;
    private readonly ParameterInfo? _parameter;

    /// <summary>
    /// 创建属性映射
    /// </summary>
    /// <param name="columnName">数据库列名（原始列名，用于日志和调试）</param>
    /// <param name="property">目标属性信息</param>
    public SimpleMemberMap(string columnName, PropertyInfo property)
    {
        _columnName = columnName;
        _property = property;
    }

    /// <summary>
    /// 创建构造函数参数映射
    /// </summary>
    /// <param name="columnName">数据库列名</param>
    /// <param name="parameter">目标构造函数参数信息</param>
    public SimpleMemberMap(string columnName, ParameterInfo parameter)
    {
        _columnName = columnName;
        _parameter = parameter;
    }

    /// <summary>
    /// 数据库列名
    /// </summary>
    public string ColumnName => _columnName;

    /// <summary>
    /// 目标属性（属性注入模式下使用）
    /// </summary>
    public PropertyInfo? Property => _property;

    /// <summary>
    /// 目标字段（当前实现不支持字段映射）
    /// </summary>
    public FieldInfo? Field => null;

    /// <summary>
    /// 目标构造函数参数（构造函数注入模式下使用）
    /// </summary>
    public ParameterInfo? Parameter => _parameter;

    /// <summary>
    /// 成员类型（属性或参数类型）
    /// </summary>
    /// <remarks>
    /// Dapper 通过此属性确定从数据库读取的值需要做何种类型转换。
    /// 例如 <c>decimal?</c> 类型会自动处理 Oracle NUMBER 到 .NET decimal 的转换，
    /// 同时处理 NULL 值的情况。
    /// </remarks>
    public Type Type => _property?.PropertyType ?? _parameter!.ParameterType;

    /// <summary>
    /// 成员类型标识（与 <see cref="Type"/> 保持一致）
    /// </summary>
    public Type MemberType => _property?.PropertyType ?? _parameter!.ParameterType;
}
