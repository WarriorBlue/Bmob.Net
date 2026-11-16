using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Bmob.Services;

namespace Bmob;

/// <summary>
/// Bmob 客户端，封装 REST API 的鉴权、请求发送与错误处理。
/// </summary>
/// <example>
/// <code>
/// var client = new BmobClient(new BmobClientOptions
/// {
///     ApplicationId = "your-app-id",
///     RestApiKey = "your-rest-api-key",
/// });
/// </code>
/// </example>
public sealed class BmobClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly bool _ownsHttpClient;
    private readonly string _baseUrl;
    private bool _disposed;

    /// <summary>
    /// 使用配置创建客户端（内部创建并管理 <see cref="HttpClient"/>）。
    /// </summary>
    public BmobClient(BmobClientOptions options)
        : this(options, null)
    {
    }

    /// <summary>
    /// 使用配置与外部 <see cref="HttpClient"/> 创建客户端。
    /// 由调用方负责该 <see cref="HttpClient"/> 的生命周期（可配合 IHttpClientFactory 使用）。
    /// </summary>
    public BmobClient(BmobClientOptions options, HttpClient? httpClient)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (string.IsNullOrWhiteSpace(options.ApplicationId))
        {
            throw new ArgumentException("必须配置 ApplicationId。", nameof(options));
        }

        if (string.IsNullOrWhiteSpace(options.RestApiKey))
        {
            throw new ArgumentException("必须配置 RestApiKey。", nameof(options));
        }

        _baseUrl = (options.BaseUrl ?? string.Empty).TrimEnd('/');
        if (!Uri.TryCreate(_baseUrl, UriKind.Absolute, out var baseUri)
            || (baseUri.Scheme != Uri.UriSchemeHttp && baseUri.Scheme != Uri.UriSchemeHttps))
        {
            throw new ArgumentException("BaseUrl 必须是合法的 HTTP/HTTPS 绝对地址。", nameof(options));
        }

        Options = options;
        _httpClient = httpClient ?? new HttpClient { Timeout = options.Timeout };
        _ownsHttpClient = httpClient is null;

        Objects = new BmobObjectService(this);
        Users = new BmobUserService(this);
        Files = new BmobFileService(this);
        Functions = new BmobFunctionService(this);
        Bql = new BmobBqlService(this);
        Batch = new BmobBatchService(this);
        Push = new BmobPushService(this);
        Sms = new BmobSmsService(this);
    }

    /// <summary>
    /// 当前配置。
    /// </summary>
    public BmobClientOptions Options { get; }

    /// <summary>
    /// 数据对象服务（增删改查、查询）。
    /// </summary>
    public BmobObjectService Objects { get; }

    /// <summary>
    /// 用户服务（注册、登录、会话、改密）。
    /// </summary>
    public BmobUserService Users { get; }

    /// <summary>
    /// 文件服务（上传、删除）。
    /// </summary>
    public BmobFileService Files { get; }

    /// <summary>
    /// 云函数服务。
    /// </summary>
    public BmobFunctionService Functions { get; }

    /// <summary>
    /// BQL 查询服务。
    /// </summary>
    public BmobBqlService Bql { get; }

    /// <summary>
    /// 批量操作服务。
    /// </summary>
    public BmobBatchService Batch { get; }

    /// <summary>
    /// 推送服务（需要 Master Key）。
    /// </summary>
    public BmobPushService Push { get; }

    /// <summary>
    /// 短信验证码服务。
    /// </summary>
    public BmobSmsService Sms { get; }

    /// <summary>
    /// 当前登录用户，由 <see cref="BmobUserService.LoginAsync"/> 等方法自动设置。
    /// 可在应用启动时用持久化的 objectId 与 SessionToken 恢复登录状态。
    /// </summary>
    public BmobUser? CurrentUser { get; set; }

    /// <summary>
    /// 快捷创建客户端。
    /// </summary>
    public static BmobClient Create(string applicationId, string restApiKey, string? masterKey = null, string? baseUrl = null)
    {
        var options = new BmobClientOptions
        {
            ApplicationId = applicationId,
            RestApiKey = restApiKey,
            MasterKey = masterKey,
        };

        if (!string.IsNullOrWhiteSpace(baseUrl))
        {
            options.BaseUrl = baseUrl;
        }

        return new BmobClient(options);
    }

    /// <summary>
    /// 发送自定义 REST 请求，用于访问 SDK 尚未封装的接口（例如角色、文件管理）。
    /// </summary>
    /// <param name="method">HTTP 方法。</param>
    /// <param name="path">以 <c>/1/</c> 或 <c>/2/</c> 开头的相对路径。</param>
    /// <param name="body">请求体，会被序列化为 JSON。</param>
    /// <param name="query">查询参数。</param>
    /// <param name="options">请求选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<JsonElement> RequestAsync(
        HttpMethod method,
        string path,
        object? body = null,
        IReadOnlyDictionary<string, string?>? query = null,
        BmobRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = query?.Select(static pair => new KeyValuePair<string, string?>(pair.Key, pair.Value));
        return SendAsync(method, path, parameters, body, options, cancellationToken);
    }

    /// <summary>
    /// 获取 Bmob 服务器时间。
    /// </summary>
    public async Task<DateTimeOffset> GetServerTimeAsync(CancellationToken cancellationToken = default)
    {
        var root = await SendAsync(HttpMethod.Get, BmobPaths.Timestamp, null, null, null, cancellationToken)
            .ConfigureAwait(false);

        if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("timestamp", out var timestamp)
                && timestamp.ValueKind == JsonValueKind.Number
                && timestamp.TryGetInt64(out var seconds))
            {
                return DateTimeOffset.FromUnixTimeSeconds(seconds);
            }

            if (root.TryGetProperty("datetime", out var datetime) && datetime.ValueKind == JsonValueKind.String)
            {
                return BmobDate.ParseIso(datetime.GetString()!);
            }
        }

        throw new BmobException("无法解析 Bmob 服务器时间响应。");
    }

    /// <inheritdoc />
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        if (_ownsHttpClient)
        {
            _httpClient.Dispose();
        }
    }

    internal async Task<JsonElement> SendAsync(
        HttpMethod method,
        string path,
        IEnumerable<KeyValuePair<string, string?>>? query,
        object? body,
        BmobRequestOptions? options,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, query, options);

        if (body is not null)
        {
            var content = new ByteArrayContent(BmobJson.Serialize(body));
            content.Headers.ContentType = new MediaTypeHeaderValue("application/json")
            {
                CharSet = "utf-8",
            };
            request.Content = content;
        }

        return await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
    }

    internal async Task<JsonElement> SendContentAsync(
        HttpMethod method,
        string path,
        HttpContent content,
        BmobRequestOptions? options,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(method, path, null, options);
        request.Content = content;
        return await SendCoreAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private HttpRequestMessage CreateRequest(
        HttpMethod method,
        string path,
        IEnumerable<KeyValuePair<string, string?>>? query,
        BmobRequestOptions? options)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (string.IsNullOrWhiteSpace(path))
        {
            throw new ArgumentException("请求路径不能为空。", nameof(path));
        }

        var request = new HttpRequestMessage(method, BuildUri(path, query));
        request.Headers.TryAddWithoutValidation("X-Bmob-Application-Id", Options.ApplicationId);
        request.Headers.TryAddWithoutValidation("X-Bmob-REST-API-Key", Options.RestApiKey);
        request.Headers.TryAddWithoutValidation("Accept", "application/json");

        if (!string.IsNullOrWhiteSpace(Options.UserAgent))
        {
            request.Headers.TryAddWithoutValidation("User-Agent", Options.UserAgent);
        }

        var useMasterKey = options?.UseMasterKey ?? Options.UseMasterKeyByDefault;
        if (useMasterKey)
        {
            if (string.IsNullOrWhiteSpace(Options.MasterKey))
            {
                throw new InvalidOperationException("未配置 MasterKey，无法使用 Master Key 发起请求。");
            }

            request.Headers.TryAddWithoutValidation("X-Bmob-Master-Key", Options.MasterKey);
        }
        else
        {
            var sessionToken = options?.SessionToken
                               ?? (options?.UseCurrentSession == true ? CurrentUser?.SessionToken : null);

            if (!string.IsNullOrWhiteSpace(sessionToken))
            {
                request.Headers.TryAddWithoutValidation("X-Bmob-Session-Token", sessionToken);
            }
        }

        return request;
    }

    private Uri BuildUri(string path, IEnumerable<KeyValuePair<string, string?>>? query)
    {
        var builder = new StringBuilder(_baseUrl.Length + path.Length + 64);
        builder.Append(_baseUrl);

        if (!path.StartsWith('/'))
        {
            builder.Append('/');
        }

        builder.Append(path);

        if (query is not null)
        {
            var separator = '?';
            foreach (var (key, value) in query)
            {
                if (value is null)
                {
                    continue;
                }

                builder.Append(separator)
                    .Append(Uri.EscapeDataString(key))
                    .Append('=')
                    .Append(Uri.EscapeDataString(value));

                separator = '&';
            }
        }

        return new Uri(builder.ToString(), UriKind.Absolute);
    }

    private async Task<JsonElement> SendCoreAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        using var response = await _httpClient
            .SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
            .ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            throw BmobException.FromResponse(response.StatusCode, body);
        }

        if (string.IsNullOrWhiteSpace(body))
        {
            return default;
        }

        try
        {
            return BmobJson.Parse(body);
        }
        catch (JsonException exception)
        {
            throw new BmobException("Bmob 返回了无法解析的响应内容。", exception);
        }
    }
}
