using System.Text;
using System.Text.Json;

namespace Bmob;

/// <summary>
/// Bmob 数据对象，对应数据表中的一条记录。
/// 字段以字典形式存储，支持任意自定义字段，同时提供强类型读取扩展。
/// </summary>
public class BmobObject
{
    internal static readonly HashSet<string> ReservedFieldNames = new(StringComparer.Ordinal)
    {
        "objectId",
        "createdAt",
        "updatedAt",
        "ACL",
        "className",
        "__type",
    };

    private readonly Dictionary<string, object?> _fields = new(StringComparer.Ordinal);
    private readonly HashSet<string> _dirtyFields = new(StringComparer.Ordinal);

    /// <summary>
    /// 使用表名创建数据对象。
    /// </summary>
    /// <param name="className">表名，例如 <c>GameScore</c>。</param>
    public BmobObject(string className)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(className);
        if (className.Contains('/') || className.Contains('?') || className.Contains('#'))
        {
            throw new ArgumentException("表名不能包含 '/', '?' 或 '#'。", nameof(className));
        }

        ClassName = className;
    }

    /// <summary>
    /// 复制一个已有对象（含字段与元数据）。
    /// </summary>
    protected BmobObject(BmobObject source)
        : this(source.ClassName)
    {
        ObjectId = source.ObjectId;
        CreatedAt = source.CreatedAt;
        UpdatedAt = source.UpdatedAt;
        Acl = source.Acl;

        foreach (var (key, value) in source._fields)
        {
            _fields[key] = value;
        }
    }

    /// <summary>
    /// 表名。
    /// </summary>
    public string ClassName { get; }

    /// <summary>
    /// 记录的 objectId，保存成功后由服务端返回。
    /// </summary>
    public string? ObjectId { get; set; }

    /// <summary>
    /// 创建时间（UTC）。
    /// </summary>
    public DateTimeOffset? CreatedAt { get; set; }

    /// <summary>
    /// 最后更新时间（UTC）。
    /// </summary>
    public DateTimeOffset? UpdatedAt { get; set; }

    /// <summary>
    /// 访问控制列表。
    /// </summary>
    public BmobAcl? Acl { get; set; }

    /// <summary>
    /// 全部自定义字段。值可能是 CLR 对象，也可能是服务端返回的 <see cref="JsonElement"/>。
    /// </summary>
    public IReadOnlyDictionary<string, object?> Fields => _fields;

    /// <summary>
    /// 自上次保存/更新以来被修改过的字段。
    /// </summary>
    public IReadOnlyCollection<string> DirtyFields => _dirtyFields;

    /// <summary>
    /// 按字段名读写值。
    /// </summary>
    /// <param name="field">字段名。</param>
    public object? this[string field]
    {
        get => GetRaw(field);
        set => Set(field, value);
    }

    /// <summary>
    /// 设置字段值。
    /// </summary>
    public BmobObject Set(string field, object? value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(field);

        if (ReservedFieldNames.Contains(field))
        {
            throw new ArgumentException($"字段名 '{field}' 是 Bmob 保留字段，不能直接赋值。", nameof(field));
        }

        _fields[field] = value;
        _dirtyFields.Add(field);
        return this;
    }

    /// <summary>
    /// 批量设置字段值。
    /// </summary>
    public BmobObject SetAll(IEnumerable<KeyValuePair<string, object?>> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        foreach (var (key, value) in fields)
        {
            Set(key, value);
        }

        return this;
    }

    /// <summary>
    /// 判断是否存在指定字段。
    /// </summary>
    public bool ContainsField(string field) => _fields.ContainsKey(field);

    /// <summary>
    /// 获取字段的原始值。
    /// </summary>
    public object? GetRaw(string field) => _fields.TryGetValue(field, out var value) ? value : null;

    /// <summary>
    /// 以强类型方式读取字段值。
    /// </summary>
    /// <typeparam name="T">目标类型，支持 Bmob 特殊类型（日期、指针、文件、地理位置等）。</typeparam>
    public T? Get<T>(string field)
    {
        if (!_fields.TryGetValue(field, out var raw) || raw is null)
        {
            return default;
        }

        if (raw is T typed)
        {
            return typed;
        }

        if (raw is JsonElement element)
        {
            if (typeof(T) == typeof(JsonElement))
            {
                return (T)(object)element;
            }

            if (typeof(T) == typeof(BmobObject))
            {
                return (T)(object)FromJson(element, null);
            }

            return element.Deserialize<T>(BmobJson.Options);
        }

        var bytes = BmobJson.Serialize(raw);
        return JsonSerializer.Deserialize<T>(bytes, BmobJson.Options);
    }

    /// <summary>
    /// 读取字符串字段。
    /// </summary>
    public string? GetString(string field) => Get<string>(field);

    /// <summary>
    /// 读取整数字段。
    /// </summary>
    public int? GetInt(string field) => Get<int?>(field);

    /// <summary>
    /// 读取长整数字段。
    /// </summary>
    public long? GetLong(string field) => Get<long?>(field);

    /// <summary>
    /// 读取浮点字段。
    /// </summary>
    public double? GetDouble(string field) => Get<double?>(field);

    /// <summary>
    /// 读取布尔字段。
    /// </summary>
    public bool? GetBool(string field) => Get<bool?>(field);

    /// <summary>
    /// 读取日期字段（UTC）。
    /// </summary>
    public DateTimeOffset? GetDateTime(string field) => Get<DateTimeOffset?>(field);

    /// <summary>
    /// 读取嵌套对象字段（对应 <c>__type=Object</c> 的展开结果）。
    /// </summary>
    public BmobObject? GetObject(string field) => Get<BmobObject>(field);

    /// <summary>
    /// 读取数组字段。
    /// </summary>
    public IReadOnlyList<T> GetList<T>(string field) => Get<List<T>>(field) ?? new List<T>();

    /// <summary>
    /// 原子计数器：字段值加上指定数量。
    /// </summary>
    public BmobObject Increment(string field, long amount = 1) => Set(field, BmobOperation.Increment(amount));

    /// <summary>
    /// 向数组字段追加元素。
    /// </summary>
    public BmobObject Add(string field, params object?[] values) => Set(field, BmobOperation.Add(values));

    /// <summary>
    /// 向数组字段追加不重复的元素。
    /// </summary>
    public BmobObject AddUnique(string field, params object?[] values) => Set(field, BmobOperation.AddUnique(values));

    /// <summary>
    /// 从数组字段移除元素。
    /// </summary>
    public BmobObject Remove(string field, params object?[] values) => Set(field, BmobOperation.Remove(values));

    /// <summary>
    /// 删除字段。
    /// </summary>
    public BmobObject DeleteField(string field) => Set(field, BmobOperation.Delete());

    /// <summary>
    /// 建立多对多关系。
    /// </summary>
    public BmobObject AddRelation(string field, params BmobObject[] targets) =>
        Set(field, BmobOperation.AddRelation(targets));

    /// <summary>
    /// 移除多对多关系。
    /// </summary>
    public BmobObject RemoveRelation(string field, params BmobObject[] targets) =>
        Set(field, BmobOperation.RemoveRelation(targets));

    /// <summary>
    /// 将当前对象映射为强类型模型（基于 System.Text.Json，公共属性按 camelCase 映射）。
    /// </summary>
    /// <typeparam name="T">目标模型类型。</typeparam>
    public T? ToObject<T>()
    {
        var bytes = ToJsonBytes(includeMetadata: true);
        return JsonSerializer.Deserialize<T>(bytes, BmobJson.Options);
    }

    /// <summary>
    /// 从 POCO 创建 Bmob 对象。
    /// </summary>
    /// <typeparam name="T">模型类型。</typeparam>
    /// <param name="className">表名。</param>
    /// <param name="value">模型实例。</param>
    public static BmobObject From<T>(string className, T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var element = JsonSerializer.SerializeToElement(value, value.GetType(), BmobJson.Options);
        return FromJson(element, className);
    }

    /// <summary>
    /// 从 JSON 解析 Bmob 对象。
    /// </summary>
    /// <param name="element">对象 JSON。</param>
    /// <param name="className">表名；未指定时尝试读取 <c>className</c> 字段。</param>
    public static BmobObject FromJson(JsonElement element, string? className = null)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException($"无法将 JSON 值解析为 Bmob 对象：{element.GetRawText()}");
        }

        if (string.IsNullOrEmpty(className)
            && element.TryGetProperty("className", out var classNameElement)
            && classNameElement.ValueKind == JsonValueKind.String)
        {
            className = classNameElement.GetString();
        }

        var result = new BmobObject(string.IsNullOrEmpty(className) ? "Object" : className);
        result.LoadFrom(element);
        return result;
    }

    /// <summary>
    /// 输出对象的 JSON 表示。
    /// </summary>
    public string ToJson(bool includeMetadata = true) => Encoding.UTF8.GetString(ToJsonBytes(includeMetadata));

    /// <inheritdoc />
    public override string ToString() => $"{ClassName}({ObjectId ?? "new"})";

    /// <summary>
    /// 解析服务端返回的对象数据。
    /// </summary>
    protected void LoadFrom(JsonElement element)
    {
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "objectId":
                    ObjectId = property.Value.ValueKind == JsonValueKind.String ? property.Value.GetString() : null;
                    break;

                case "createdAt":
                    CreatedAt = TryParseDate(property.Value);
                    break;

                case "updatedAt":
                    UpdatedAt = TryParseDate(property.Value);
                    break;

                case "ACL":
                    Acl = property.Value.ValueKind == JsonValueKind.Object ? BmobAcl.Parse(property.Value) : null;
                    break;

                case "className":
                case "__type":
                    break;

                default:
                    _fields[property.Name] = property.Value.Clone();
                    break;
            }
        }
    }

    internal void WriteFieldsTo(Utf8JsonWriter writer) => BmobJson.WriteValue(writer, CreatePayload());

    internal byte[] ToJsonBytes(bool includeMetadata)
    {
        var payload = CreatePayload();

        if (includeMetadata)
        {
            payload["className"] = ClassName;

            if (!string.IsNullOrEmpty(ObjectId))
            {
                payload["objectId"] = ObjectId;
            }

            if (CreatedAt.HasValue)
            {
                payload["createdAt"] = CreatedAt.Value;
            }

            if (UpdatedAt.HasValue)
            {
                payload["updatedAt"] = UpdatedAt.Value;
            }
        }

        return BmobJson.Serialize(payload);
    }

    internal Dictionary<string, object?> CreatePayload()
    {
        var payload = new Dictionary<string, object?>(_fields, StringComparer.Ordinal);
        if (Acl is not null)
        {
            payload["ACL"] = Acl;
        }

        return payload;
    }

    internal Dictionary<string, object?> UpdatePayload(IEnumerable<string>? fields = null)
    {
        var requested = fields?.ToList();

        List<string> keys;
        if (requested is { Count: > 0 })
        {
            keys = requested;
        }
        else if (_dirtyFields.Count > 0)
        {
            keys = _dirtyFields.ToList();
        }
        else
        {
            keys = _fields.Keys.ToList();
        }

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var key in keys)
        {
            if (ReservedFieldNames.Contains(key))
            {
                throw new ArgumentException($"字段名 '{key}' 是 Bmob 保留字段，不能用于更新。", nameof(fields));
            }

            if (_fields.TryGetValue(key, out var value))
            {
                payload[key] = value;
            }
        }

        if (Acl is not null && (requested is null || requested.Contains("ACL", StringComparer.Ordinal)))
        {
            payload["ACL"] = Acl;
        }

        if (payload.Count == 0)
        {
            throw new InvalidOperationException("没有可更新的字段，请先调用 Set 设置字段值。");
        }

        return payload;
    }

    internal void ApplyResult(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (element.TryGetProperty("objectId", out var objectId) && objectId.ValueKind == JsonValueKind.String)
        {
            ObjectId = objectId.GetString();
        }

        if (element.TryGetProperty("createdAt", out var createdAt))
        {
            CreatedAt = TryParseDate(createdAt) ?? CreatedAt;
        }

        if (element.TryGetProperty("updatedAt", out var updatedAt))
        {
            UpdatedAt = TryParseDate(updatedAt) ?? UpdatedAt;
        }
    }

    internal void ClearDirtyFields() => _dirtyFields.Clear();

    internal void RemoveField(string field) => _fields.Remove(field);

    private static DateTimeOffset? TryParseDate(JsonElement element)
    {
        try
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => BmobDate.ParseIso(element.GetString()!),
                JsonValueKind.Object => BmobDate.Parse(element).Value,
                _ => null,
            };
        }
        catch (FormatException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}
