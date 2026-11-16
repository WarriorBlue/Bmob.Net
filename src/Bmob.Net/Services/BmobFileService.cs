using System.Net.Http.Headers;
using System.Text.Json;

namespace Bmob.Services;

/// <summary>
/// 文件服务，封装 <c>/2/files</c> 上传与删除接口。
/// </summary>
public sealed class BmobFileService
{
    private static readonly HttpClient DownloadClient = new();

    private readonly BmobClient _client;

    internal BmobFileService(BmobClient client)
    {
        _client = client;
    }

    /// <summary>
    /// 上传本地文件。
    /// </summary>
    /// <param name="filePath">本地文件路径。</param>
    /// <param name="fileName">上传后的文件名，默认使用本地文件名。</param>
    /// <param name="contentType">MIME 类型，默认按扩展名推断。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<BmobFile> UploadAsync(
        string filePath,
        string? fileName = null,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var data = await File.ReadAllBytesAsync(filePath, cancellationToken).ConfigureAwait(false);
        var name = fileName ?? Path.GetFileName(filePath);
        return await UploadAsync(data, name, contentType ?? BmobMimeTypes.FromFileName(filePath), cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 上传字节数组。调用方保留数据所有权。
    /// </summary>
    public async Task<BmobFile> UploadAsync(
        byte[] data,
        string fileName,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        using var content = new ByteArrayContent(data);
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType ?? BmobMimeTypes.FromFileName(fileName));

        return await UploadCoreAsync(content, fileName, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 上传流。可定位的流直接流式上传（不额外占用内存），不可定位的流会先缓冲到内存；完成后不关闭调用方的流。
    /// </summary>
    public async Task<BmobFile> UploadAsync(
        Stream stream,
        string fileName,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);

        var mimeType = contentType ?? BmobMimeTypes.FromFileName(fileName);

        if (stream.CanSeek)
        {
            var content = new StreamContent(new NonDisposingStream(stream), bufferSize: 81920);
            content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
            return await UploadCoreAsync(content, fileName, cancellationToken).ConfigureAwait(false);
        }

        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return await UploadAsync(buffer.ToArray(), fileName, mimeType, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 删除文件（根据上传返回的 <see cref="BmobFile.Cdn"/> 与 URL 推导存储路径）。
    /// </summary>
    public async Task DeleteAsync(BmobFile file, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(file);

        if (string.IsNullOrWhiteSpace(file.Cdn))
        {
            throw new InvalidOperationException("缺少 Cdn 标识，无法删除文件。");
        }

        if (string.IsNullOrWhiteSpace(file.Url))
        {
            throw new InvalidOperationException("缺少文件 URL，无法删除文件。");
        }

        await DeleteAsync(file.Cdn, ExtractRelativePath(file.Url), cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 按存储服务商标识与相对路径删除文件。
    /// </summary>
    /// <param name="cdnName">上传接口返回的 <c>cdn</c>，例如 <c>aliyun</c>。</param>
    /// <param name="relativePath">去掉域名后的文件路径，例如 <c>2024/01/01/xxx.png</c>。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task DeleteAsync(string cdnName, string relativePath, CancellationToken cancellationToken = default)
    {
        await _client
            .SendAsync(HttpMethod.Delete, BmobPaths.FileWithCdn(cdnName, relativePath), null, null, null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 通过文件 URL 下载文件内容（普通 HTTP GET，不经过 Bmob 鉴权）。
    /// </summary>
    public async Task<byte[]> DownloadAsync(string url, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);

        return await DownloadClient.GetByteArrayAsync(url, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 通过文件 URL 流式下载文件到本地路径（不整段载入内存）。
    /// </summary>
    /// <returns>下载完成后的绝对路径。</returns>
    public async Task<string> DownloadToFileAsync(
        string url,
        string filePath,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(url);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);

        var fullPath = Path.GetFullPath(filePath);
        var directory = Path.GetDirectoryName(fullPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        using var response = await DownloadClient
            .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        response.EnsureSuccessStatusCode();

        await using var source = await response.Content
            .ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);

        await using var target = new FileStream(
            fullPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);

        await source.CopyToAsync(target, cancellationToken).ConfigureAwait(false);
        return fullPath;
    }

    private async Task<BmobFile> UploadCoreAsync(
        HttpContent content,
        string fileName,
        CancellationToken cancellationToken)
    {
        var root = await _client
            .SendContentAsync(HttpMethod.Post, BmobPaths.File(fileName), content, null, cancellationToken)
            .ConfigureAwait(false);

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new BmobException("Bmob 返回的文件信息格式不正确。");
        }

        return BmobFile.FromJson(root);
    }

    private static string ExtractRelativePath(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return uri.AbsolutePath.TrimStart('/');
        }

        return url.TrimStart('/');
    }
}

/// <summary>
/// 常见文件扩展名到 MIME 类型的映射。
/// </summary>
internal static class BmobMimeTypes
{
    private static readonly Dictionary<string, string> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".bmp"] = "image/bmp",
        [".svg"] = "image/svg+xml",
        [".ico"] = "image/x-icon",
        [".pdf"] = "application/pdf",
        [".txt"] = "text/plain",
        [".json"] = "application/json",
        [".xml"] = "application/xml",
        [".html"] = "text/html",
        [".css"] = "text/css",
        [".js"] = "application/javascript",
        [".zip"] = "application/zip",
        [".gz"] = "application/gzip",
        [".mp3"] = "audio/mpeg",
        [".wav"] = "audio/wav",
        [".mp4"] = "video/mp4",
        [".mov"] = "video/quicktime",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
        [".ppt"] = "application/vnd.ms-powerpoint",
        [".pptx"] = "application/vnd.openxmlformats-officedocument.presentationml.presentation",
        [".csv"] = "text/csv",
    };

    internal static string FromFileName(string fileName)
    {
        var extension = Path.GetExtension(fileName);
        return !string.IsNullOrEmpty(extension) && Map.TryGetValue(extension, out var contentType)
            ? contentType
            : "application/octet-stream";
    }
}

/// <summary>
/// 包装调用方的流，确保 HttpClient 释放请求内容时不会关闭底层流。
/// </summary>
internal sealed class NonDisposingStream : Stream
{
    private readonly Stream _inner;

    public NonDisposingStream(Stream inner) => _inner = inner;

    public override bool CanRead => _inner.CanRead;

    public override bool CanSeek => _inner.CanSeek;

    public override bool CanWrite => _inner.CanWrite;

    public override long Length => _inner.Length;

    public override long Position
    {
        get => _inner.Position;
        set => _inner.Position = value;
    }

    public override void Flush() => _inner.Flush();

    public override int Read(byte[] buffer, int offset, int count) => _inner.Read(buffer, offset, count);

    public override long Seek(long offset, SeekOrigin origin) => _inner.Seek(offset, origin);

    public override void SetLength(long value) => _inner.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

    public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
        _inner.ReadAsync(buffer, cancellationToken);

    public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default) =>
        _inner.WriteAsync(buffer, cancellationToken);

    protected override void Dispose(bool disposing)
    {
        // HttpClient 发送完成后会自动释放 HttpContent，这里不能连带释放调用方的流。
    }
}
