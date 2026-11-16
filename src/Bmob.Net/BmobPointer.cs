using System.Text.Json;

namespace Bmob;

/// <summary>
/// Bmob 指针类型，用于关联另一张表中的单条记录。
/// 序列化为 <c>{"__type":"Pointer","className":"Game","objectId":"xxx"}</c>。
/// </summary>
public sealed class BmobPointer : IBmobValue, IEquatable<BmobPointer>
{
    /// <summary>
    /// 使用类名与 objectId 创建指针。
    /// </summary>
    public BmobPointer(string className, string objectId)
    {
        if (string.IsNullOrWhiteSpace(className))
        {
            throw new ArgumentException("类名不能为空。", nameof(className));
        }

        if (string.IsNullOrWhiteSpace(objectId))
        {
            throw new ArgumentException("objectId 不能为空。", nameof(objectId));
        }

        ClassName = className;
        ObjectId = objectId;
    }

    /// <summary>
    /// 使用一个已保存的 Bmob 对象创建指针。
    /// </summary>
    public BmobPointer(BmobObject target)
    {
        ArgumentNullException.ThrowIfNull(target);
        if (string.IsNullOrEmpty(target.ObjectId))
        {
            throw new ArgumentException("目标对象尚未保存（缺少 objectId）。", nameof(target));
        }

        ClassName = target.ClassName;
        ObjectId = target.ObjectId;
    }

    /// <summary>
    /// 指针指向的表名。
    /// </summary>
    public string ClassName { get; }

    /// <summary>
    /// 指针指向的记录 objectId。
    /// </summary>
    public string ObjectId { get; }

    /// <summary>
    /// 解析指针 JSON（支持展开后的 <c>__type=Object</c>）。
    /// </summary>
    public static BmobPointer Parse(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException($"无法将 JSON 值解析为 Bmob 指针：{element.GetRawText()}");
        }

        var className = element.TryGetProperty("className", out var classNameElement) ? classNameElement.GetString() : null;
        var objectId = element.TryGetProperty("objectId", out var objectIdElement) ? objectIdElement.GetString() : null;

        if (string.IsNullOrEmpty(className) || string.IsNullOrEmpty(objectId))
        {
            throw new FormatException($"Bmob 指针缺少 className 或 objectId：{element.GetRawText()}");
        }

        return new BmobPointer(className, objectId);
    }

    /// <inheritdoc />
    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("__type", "Pointer");
        writer.WriteString("className", ClassName);
        writer.WriteString("objectId", ObjectId);
        writer.WriteEndObject();
    }

    /// <inheritdoc />
    public bool Equals(BmobPointer? other) =>
        other is not null && ClassName == other.ClassName && ObjectId == other.ObjectId;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is BmobPointer other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(ClassName, ObjectId);

    /// <inheritdoc />
    public override string ToString() => $"{ClassName}:{ObjectId}";
}
