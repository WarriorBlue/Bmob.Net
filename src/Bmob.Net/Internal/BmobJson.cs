using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Bmob.Serialization;

namespace Bmob;

/// <summary>
/// SDK 内部统一的 JSON 序列化入口，负责处理 Bmob 特殊数据类型。
/// </summary>
internal static class BmobJson
{
    private static readonly JsonWriterOptions WriterOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Indented = false,
    };

    internal static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
        };

        options.Converters.Add(new BmobDateTimeOffsetJsonConverter());
        options.Converters.Add(new BmobDateTimeJsonConverter());
        options.Converters.Add(new BmobObjectJsonConverter());
        options.Converters.Add(new BmobDateJsonConverter());
        options.Converters.Add(new BmobPointerJsonConverter());
        options.Converters.Add(new BmobFileJsonConverter());
        options.Converters.Add(new BmobGeoPointJsonConverter());
        options.Converters.Add(new BmobRelationJsonConverter());
        options.Converters.Add(new BmobAclJsonConverter());
        options.Converters.Add(new BmobOperationJsonConverter());
        return options;
    }

    /// <summary>
    /// 将任意值序列化为 UTF-8 JSON。
    /// </summary>
    internal static byte[] Serialize(object? value)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer, WriterOptions))
        {
            WriteValue(writer, value);
        }

        return buffer.ToArray();
    }

    /// <summary>
    /// 将任意值序列化为 JSON 字符串。
    /// </summary>
    internal static string SerializeToString(object? value) => Encoding.UTF8.GetString(Serialize(value));

    /// <summary>
    /// 解析 JSON 文本，返回与文档生命周期无关的 <see cref="JsonElement"/>。
    /// </summary>
    internal static JsonElement Parse(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    /// <summary>
    /// 按 Bmob 约定写入任意值。
    /// </summary>
    internal static void WriteValue(Utf8JsonWriter writer, object? value)
    {
        switch (value)
        {
            case null:
                writer.WriteNullValue();
                return;

            case JsonElement element:
                if (element.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
                {
                    writer.WriteNullValue();
                }
                else
                {
                    element.WriteTo(writer);
                }

                return;

            case JsonNode node:
                node.WriteTo(writer);
                return;

            case BmobObject bmobObject:
                bmobObject.WriteFieldsTo(writer);
                return;

            case IBmobValue bmobValue:
                bmobValue.WriteTo(writer);
                return;

            case string text:
                writer.WriteStringValue(text);
                return;

            case bool boolean:
                writer.WriteBooleanValue(boolean);
                return;

            case DateTimeOffset dateTimeOffset:
                WriteDate(writer, dateTimeOffset);
                return;

            case DateTime dateTime:
                WriteDate(writer, dateTime.Kind == DateTimeKind.Utc
                    ? new DateTimeOffset(dateTime)
                    : new DateTimeOffset(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc)));
                return;

            case Guid guid:
                writer.WriteStringValue(guid.ToString());
                return;

            case char character:
                writer.WriteStringValue(character.ToString());
                return;

            case Enum enumValue:
                writer.WriteStringValue(enumValue.ToString());
                return;

            case byte or sbyte or short or ushort or int or uint or long:
                writer.WriteNumberValue(Convert.ToInt64(value, CultureInfo.InvariantCulture));
                return;

            case ulong unsignedLong:
                writer.WriteNumberValue(unsignedLong);
                return;

            case float single:
                writer.WriteNumberValue(single);
                return;

            case double number:
                writer.WriteNumberValue(number);
                return;

            case decimal money:
                writer.WriteNumberValue(money);
                return;

            case IDictionary<string, object?> dictionary:
                writer.WriteStartObject();
                foreach (var (key, item) in dictionary)
                {
                    writer.WritePropertyName(key);
                    WriteValue(writer, item);
                }

                writer.WriteEndObject();
                return;

            case IDictionary dictionary:
                writer.WriteStartObject();
                foreach (DictionaryEntry entry in dictionary)
                {
                    writer.WritePropertyName(Convert.ToString(entry.Key, CultureInfo.InvariantCulture) ?? string.Empty);
                    WriteValue(writer, entry.Value);
                }

                writer.WriteEndObject();
                return;

            case IEnumerable enumerable:
                writer.WriteStartArray();
                foreach (var item in enumerable)
                {
                    WriteValue(writer, item);
                }

                writer.WriteEndArray();
                return;

            default:
                JsonSerializer.Serialize(writer, value, value.GetType(), Options);
                return;
        }
    }

    private static void WriteDate(Utf8JsonWriter writer, DateTimeOffset value)
    {
        writer.WriteStartObject();
        writer.WriteString("__type", "Date");
        writer.WriteString("iso", BmobDate.ToIso(value));
        writer.WriteEndObject();
    }
}
