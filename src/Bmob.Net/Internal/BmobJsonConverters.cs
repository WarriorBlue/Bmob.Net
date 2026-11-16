using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bmob.Serialization;

internal sealed class BmobObjectJsonConverter : JsonConverter<BmobObject>
{
    public override BmobObject Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return BmobObject.FromJson(document.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, BmobObject value, JsonSerializerOptions options) =>
        value.WriteFieldsTo(writer);
}

internal sealed class BmobDateJsonConverter : JsonConverter<BmobDate>
{
    public override BmobDate Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return BmobDate.Parse(document.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, BmobDate value, JsonSerializerOptions options) => value.WriteTo(writer);
}

internal sealed class BmobPointerJsonConverter : JsonConverter<BmobPointer>
{
    public override BmobPointer Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return BmobPointer.Parse(document.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, BmobPointer value, JsonSerializerOptions options) => value.WriteTo(writer);
}

internal sealed class BmobFileJsonConverter : JsonConverter<BmobFile>
{
    public override BmobFile Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return BmobFile.FromJson(document.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, BmobFile value, JsonSerializerOptions options) => value.WriteTo(writer);
}

internal sealed class BmobGeoPointJsonConverter : JsonConverter<BmobGeoPoint>
{
    public override BmobGeoPoint Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return BmobGeoPoint.Parse(document.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, BmobGeoPoint value, JsonSerializerOptions options) => value.WriteTo(writer);
}

internal sealed class BmobRelationJsonConverter : JsonConverter<BmobRelation>
{
    public override BmobRelation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return BmobRelation.Parse(document.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, BmobRelation value, JsonSerializerOptions options) => value.WriteTo(writer);
}

internal sealed class BmobAclJsonConverter : JsonConverter<BmobAcl>
{
    public override BmobAcl Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        return BmobAcl.Parse(document.RootElement);
    }

    public override void Write(Utf8JsonWriter writer, BmobAcl value, JsonSerializerOptions options) => value.WriteTo(writer);
}

internal sealed class BmobOperationJsonConverter : JsonConverter<BmobOperation>
{
    public override BmobOperation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        throw new NotSupportedException("BmobOperation 仅用于写入，不能从 JSON 反序列化。");

    public override void Write(Utf8JsonWriter writer, BmobOperation value, JsonSerializerOptions options) => value.WriteTo(writer);
}

internal sealed class BmobDateTimeOffsetJsonConverter : JsonConverter<DateTimeOffset>
{
    public override DateTimeOffset Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return BmobDate.ParseIso(reader.GetString()!);
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return BmobDate.Parse(document.RootElement).Value;
        }

        throw new JsonException($"无法将 JSON 值解析为日期：{reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, DateTimeOffset value, JsonSerializerOptions options)
    {
        writer.WriteStartObject();
        writer.WriteString("__type", "Date");
        writer.WriteString("iso", BmobDate.ToIso(value));
        writer.WriteEndObject();
    }
}

internal sealed class BmobDateTimeJsonConverter : JsonConverter<DateTime>
{
    public override DateTime Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String)
        {
            return BmobDate.ParseIso(reader.GetString()!).UtcDateTime;
        }

        if (reader.TokenType == JsonTokenType.StartObject)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            return BmobDate.Parse(document.RootElement).Value.UtcDateTime;
        }

        throw new JsonException($"无法将 JSON 值解析为日期：{reader.TokenType}");
    }

    public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options)
    {
        var dateTimeOffset = value.Kind == DateTimeKind.Utc
            ? new DateTimeOffset(value)
            : new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));

        writer.WriteStartObject();
        writer.WriteString("__type", "Date");
        writer.WriteString("iso", BmobDate.ToIso(dateTimeOffset));
        writer.WriteEndObject();
    }
}
