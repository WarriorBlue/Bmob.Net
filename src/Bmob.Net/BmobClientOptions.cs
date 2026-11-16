namespace Bmob;

/// <summary>
/// <see cref="BmobClient"/> 的配置选项。
/// </summary>
public sealed class BmobClientOptions
{
    /// <summary>
    /// 应用的 Application ID。
    /// </summary>
    public string ApplicationId { get; set; } = string.Empty;

    /// <summary>
    /// 应用的 REST API Key。
    /// </summary>
    public string RestApiKey { get; set; } = string.Empty;

    /// <summary>
    /// 应用的 Master Key。配置后可通过 <see cref="UseMasterKeyByDefault"/> 或
    /// <see cref="BmobRequestOptions.UseMasterKey"/> 绕过 ACL，仅限服务端使用。
    /// </summary>
    public string? MasterKey { get; set; }

    /// <summary>
    /// API 根地址。新版本 Bmob 应用通常需要填写控制台绑定的已备案域名，
    /// 默认值为 <c>https://api.bmobcloud.com</c>。
    /// </summary>
    public string BaseUrl { get; set; } = "https://api.bmobcloud.com";

    /// <summary>
    /// HTTP 请求超时时间，默认 30 秒。传入自定义 <see cref="System.Net.Http.HttpClient"/> 时由该客户端决定。
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// User-Agent 标识。
    /// </summary>
    public string? UserAgent { get; set; } = "Bmob.Net/1.0.0";

    /// <summary>
    /// 是否默认使用 Master Key 发起请求（服务端场景）。
    /// </summary>
    public bool UseMasterKeyByDefault { get; set; }
}

/// <summary>
/// 单次请求的附加选项。
/// </summary>
public sealed class BmobRequestOptions
{
    /// <summary>
    /// 是否使用 Master Key 发起本次请求（需要客户端配置了 Master Key）。
    /// </summary>
    public bool UseMasterKey { get; set; }

    /// <summary>
    /// 显式指定 Session Token。
    /// </summary>
    public string? SessionToken { get; set; }

    /// <summary>
    /// 是否自动附带 <see cref="BmobClient.CurrentUser"/> 的 Session Token。
    /// </summary>
    public bool UseCurrentSession { get; set; }

    /// <summary>
    /// 使用 Master Key 的请求选项。
    /// </summary>
    public static BmobRequestOptions Master => new() { UseMasterKey = true };

    /// <summary>
    /// 使用当前登录用户的请求选项。
    /// </summary>
    public static BmobRequestOptions CurrentSession => new() { UseCurrentSession = true };
}
