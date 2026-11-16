using System.Globalization;
using System.Text.Json;

namespace Bmob;

/// <summary>
/// Bmob 原子操作（<c>__op</c>），用于更新数据时执行服务端原子计算。
/// </summary>
public sealed class BmobOperation : IBmobValue
{
    private readonly string _operation;
    private readonly object? _value;

    private BmobOperation(string operation, object? value)
    {
        _operation = operation;
        _value = value;
    }

    /// <summary>
    /// 原子计数器：<c>{"__op":"Increment","amount":1}</c>。
    /// </summary>
    public static BmobOperation Increment(long amount = 1) => new("Increment", amount);

    /// <summary>
    /// 原子计数器（浮点）。
    /// </summary>
    public static BmobOperation Increment(double amount) => new("Increment", amount);

    /// <summary>
    /// 向数组尾部追加元素。
    /// </summary>
    public static BmobOperation Add(params object?[] values) => new("Add", values);

    /// <summary>
    /// 仅当元素不存在时加入数组。
    /// </summary>
    public static BmobOperation AddUnique(params object?[] values) => new("AddUnique", values);

    /// <summary>
    /// 从数组移除元素。
    /// </summary>
    public static BmobOperation Remove(params object?[] values) => new("Remove", values);

    /// <summary>
    /// 删除字段：<c>{"__op":"Delete"}</c>。
    /// </summary>
    public static BmobOperation Delete() => new("Delete", null);

    /// <summary>
    /// 建立多对多关系。
    /// </summary>
    public static BmobOperation AddRelation(params BmobObject[] targets) =>
        new("AddRelation", ToPointers(targets));

    /// <summary>
    /// 建立多对多关系（使用指针）。
    /// </summary>
    public static BmobOperation AddRelation(params BmobPointer[] targets) =>
        new("AddRelation", targets);

    /// <summary>
    /// 移除多对多关系。
    /// </summary>
    public static BmobOperation RemoveRelation(params BmobObject[] targets) =>
        new("RemoveRelation", ToPointers(targets));

    /// <summary>
    /// 移除多对多关系（使用指针）。
    /// </summary>
    public static BmobOperation RemoveRelation(params BmobPointer[] targets) =>
        new("RemoveRelation", targets);

    /// <inheritdoc />
    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("__op", _operation);

        if (_operation == "Increment")
        {
            writer.WritePropertyName("amount");
            BmobJson.WriteValue(writer, _value);
        }
        else if (_operation is "Add" or "AddUnique" or "Remove" or "AddRelation" or "RemoveRelation")
        {
            writer.WritePropertyName("objects");
            BmobJson.WriteValue(writer, _value);
        }

        writer.WriteEndObject();
    }

    /// <inheritdoc />
    public override string ToString() =>
        _operation == "Increment"
            ? string.Create(CultureInfo.InvariantCulture, $"Increment({_value})")
            : _operation;

    private static BmobPointer[] ToPointers(BmobObject[] targets)
    {
        ArgumentNullException.ThrowIfNull(targets);
        return Array.ConvertAll(targets, static target => new BmobPointer(target));
    }
}
