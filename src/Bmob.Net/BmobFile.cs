using System.Text.Json;

namespace Bmob;

/// <summary>
/// Bmob 文件对象，对应上传接口的返回值，也可作为 <c>File</c> 字段写入数据表。
/// </summary>
public sealed class BmobFile : IBmobValue
{
    /// <summary>
    /// 初始化一个空文件对象。
    /// </summary>
    public BmobFile()
    {
    }

    /// <summary>
    /// 使用文件 URL 创建文件引用。
    /// </summary>
    public BmobFile(string url, string? filename = null, string? cdn = null)
    {
        Url = url;
        Filename = filename;
        Cdn = cdn;
    }

    /// <summary>
    /// 文件名。
    /// </summary>
    public string? Filename { get; set; }

    /// <summary>
    /// 文件的可访问 URL。
    /// </summary>
    public string? Url { get; set; }

    /// <summary>
    /// 文件存储服务商标识（例如 <c>aliyun</c>、<c>upyun</c>），删除文件时需要。
    /// </summary>
    public string? Cdn { get; set; }

    /// <summary>
    /// 文件分组（历史字段）。
    /// </summary>
    public string? Group { get; set; }

    /// <summary>
    /// 从上传接口的响应解析文件信息。
    /// </summary>
    public static BmobFile FromJson(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException($"无法将 JSON 值解析为 Bmob 文件：{element.GetRawText()}");
        }

        return new BmobFile
        {
            Filename = element.TryGetProperty("filename", out var filename) ? filename.GetString() : null,
            Url = element.TryGetProperty("url", out var url) ? url.GetString() : null,
            Cdn = element.TryGetProperty("cdn", out var cdn) ? cdn.GetString() : null,
            Group = element.TryGetProperty("group", out var group) ? group.GetString() : null,
        };
    }

    /// <inheritdoc />
    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        writer.WriteString("__type", "File");
        if (!string.IsNullOrEmpty(Filename))
        {
            writer.WriteString("filename", Filename);
        }

        if (!string.IsNullOrEmpty(Group))
        {
            writer.WriteString("group", Group);
        }

        if (!string.IsNullOrEmpty(Url))
        {
            writer.WriteString("url", Url);
        }

        writer.WriteEndObject();
    }

    /// <inheritdoc />
    public override string ToString() => Url ?? Filename ?? "BmobFile";
}
