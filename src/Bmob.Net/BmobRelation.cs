using System.Text.Json;

namespace Bmob;

/// <summary>
/// Bmob 多对多关系类型，序列化为 <c>{"__type":"Relation","className":"_User"}</c>。
/// 通常通过 <c>BmobOperation.AddRelation</c> / <c>BmobOperation.RemoveRelation</c> 维护。
/// </summary>
public sealed class BmobRelation : IBmobValue
{
    /// <summary>
    /// 使用关联表名创建关系。
    /// </summary>
    public BmobRelation(string className)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(className);
        ClassName = className;
    }
    /// <summary>
    /// 关联表名。
    /// </summary>
    public string ClassName { get; }

    /// <summary>
    /// 解析关系 JSON。
    /// </summary>
    public static BmobRelation Parse(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object
            || !element.TryGetProperty("className", out var className)
            || className.ValueKind != JsonValueKind.String)
        {
            throw new FormatException($"无法将 JSON 值解析为 Bmob 关系：{element.GetRawText()}");
        }

        return new BmobRelation(className.GetString()!);
    }

    /// <inheritdoc />
    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("__type", "Relation");
        writer.WriteString("className", ClassName);
        writer.WriteEndObject();
    }

    /// <inheritdoc />
    public override string ToString() => $"Relation({ClassName})";
}
