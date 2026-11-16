using System.Globalization;
using System.Text.Json;

namespace Bmob;

/// <summary>
/// Bmob 地理位置类型，序列化为 <c>{"__type":"GeoPoint","latitude":..,"longitude":..}</c>。
/// </summary>
public sealed class BmobGeoPoint : IBmobValue, IEquatable<BmobGeoPoint>
{
    /// <summary>
    /// 使用纬度、经度创建地理位置。
    /// </summary>
    /// <param name="latitude">纬度，范围 -90 ~ 90。</param>
    /// <param name="longitude">经度，范围 -180 ~ 180。</param>
    public BmobGeoPoint(double latitude, double longitude)
    {
        if (latitude is < -90 or > 90)
        {
            throw new ArgumentOutOfRangeException(nameof(latitude), latitude, "纬度必须在 -90 到 90 之间。");
        }

        if (longitude is < -180 or > 180)
        {
            throw new ArgumentOutOfRangeException(nameof(longitude), longitude, "经度必须在 -180 到 180 之间。");
        }

        Latitude = latitude;
        Longitude = longitude;
    }

    /// <summary>
    /// 纬度。
    /// </summary>
    public double Latitude { get; }

    /// <summary>
    /// 经度。
    /// </summary>
    public double Longitude { get; }

    /// <summary>
    /// 解析地理位置 JSON。
    /// </summary>
    public static BmobGeoPoint Parse(JsonElement element)
    {
        var latitude = ReadDouble(element, "latitude");
        var longitude = ReadDouble(element, "longitude");
        return new BmobGeoPoint(latitude, longitude);
    }

    /// <inheritdoc />
    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("__type", "GeoPoint");
        writer.WriteNumber("latitude", Latitude);
        writer.WriteNumber("longitude", Longitude);
        writer.WriteEndObject();
    }

    /// <inheritdoc />
    public bool Equals(BmobGeoPoint? other) =>
        other is not null && Latitude.Equals(other.Latitude) && Longitude.Equals(other.Longitude);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is BmobGeoPoint other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Latitude, Longitude);

    /// <inheritdoc />
    public override string ToString() =>
        string.Create(CultureInfo.InvariantCulture, $"({Latitude}, {Longitude})");

    private static double ReadDouble(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(propertyName, out var value))
        {
            throw new FormatException($"Bmob GeoPoint 缺少 {propertyName}：{element.GetRawText()}");
        }

        return value.ValueKind == JsonValueKind.Number
            ? value.GetDouble()
            : double.Parse(value.GetString()!, CultureInfo.InvariantCulture);
    }
}
