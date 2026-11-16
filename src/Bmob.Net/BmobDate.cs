using System.Globalization;
using System.Text.Json;

namespace Bmob;

/// <summary>
/// Bmob 日期类型，序列化为 <c>{"__type":"Date","iso":"yyyy-MM-dd HH:mm:ss"}</c>（UTC）。
/// </summary>
public sealed class BmobDate : IBmobValue, IEquatable<BmobDate>
{
    /// <summary>
    /// Bmob 使用的日期格式。
    /// </summary>
    public const string Format = "yyyy-MM-dd HH:mm:ss";

    /// <summary>
    /// 使用一个时间点创建日期。
    /// </summary>
    public BmobDate(DateTimeOffset value)
    {
        Value = value.ToUniversalTime();
    }

    /// <summary>
    /// 使用本地/UTC <see cref="DateTime"/> 创建日期。
    /// </summary>
    public BmobDate(DateTime value)
        : this(ToDateTimeOffset(value))
    {
    }

    /// <summary>
    /// 日期时间值（始终为 UTC）。
    /// </summary>
    public DateTimeOffset Value { get; }

    /// <summary>
    /// Bmob 格式的字符串表示，例如 <c>2011-08-20 02:06:57</c>。
    /// </summary>
    public string Iso => ToIso(Value);

    /// <summary>
    /// 当前 UTC 时间。
    /// </summary>
    public static BmobDate Now => new(DateTimeOffset.UtcNow);

    /// <inheritdoc />
    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("__type", "Date");
        writer.WriteString("iso", Iso);
        writer.WriteEndObject();
    }

    /// <summary>
    /// 解析 Bmob 返回的日期值（支持字符串与 <c>{"__type":"Date"}</c> 对象）。
    /// </summary>
    public static BmobDate Parse(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            return new BmobDate(ParseIso(element.GetString()!));
        }

        if (element.ValueKind == JsonValueKind.Object
            && element.TryGetProperty("iso", out var iso)
            && iso.ValueKind == JsonValueKind.String)
        {
            return new BmobDate(ParseIso(iso.GetString()!));
        }

        throw new FormatException($"无法将 JSON 值解析为 Bmob 日期：{element.GetRawText()}");
    }

    /// <summary>
    /// 解析 Bmob 日期字符串。
    /// </summary>
    public static DateTimeOffset ParseIso(string iso)
    {
        if (DateTimeOffset.TryParseExact(
                iso,
                new[] { Format, "yyyy-MM-dd HH:mm:ss.FFFFFFF", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", "O" },
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var value))
        {
            return value;
        }

        if (DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out value))
        {
            return value;
        }

        throw new FormatException($"无法解析 Bmob 日期字符串：{iso}");
    }

    /// <summary>
    /// 格式化为 Bmob 日期字符串。
    /// </summary>
    public static string ToIso(DateTimeOffset value) =>
        value.ToUniversalTime().ToString(Format, CultureInfo.InvariantCulture);

    /// <inheritdoc />
    public bool Equals(BmobDate? other) => other is not null && Value.Equals(other.Value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is BmobDate other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => Value.GetHashCode();

    /// <inheritdoc />
    public override string ToString() => Iso;

    private static DateTimeOffset ToDateTimeOffset(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => new DateTimeOffset(value),
        DateTimeKind.Local => new DateTimeOffset(value).ToUniversalTime(),
        _ => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)),
    };
}
