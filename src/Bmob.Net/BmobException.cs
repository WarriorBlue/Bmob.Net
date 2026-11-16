using System.Net;
using System.Text.Json;

namespace Bmob;

/// <summary>
/// Bmob 请求异常，包含 HTTP 状态码与 Bmob 业务错误码。
/// </summary>
public class BmobException : Exception
{
    /// <summary>
    /// 创建异常。
    /// </summary>
    public BmobException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// 创建异常。
    /// </summary>
    public BmobException(string message, Exception innerException)
        : base(message, innerException)
    {
    }

    /// <summary>
    /// 创建异常。
    /// </summary>
    public BmobException(int code, HttpStatusCode? statusCode, string? error, string? responseBody, string message)
        : base(message)
    {
        Code = code;
        StatusCode = statusCode;
        Error = error;
        ResponseBody = responseBody;
    }

    /// <summary>
    /// Bmob 业务错误码（例如 101 表示对象不存在），无法解析时等于 HTTP 状态码。
    /// </summary>
    public int Code { get; }

    /// <summary>
    /// HTTP 状态码。
    /// </summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>
    /// 服务端返回的错误描述。
    /// </summary>
    public string? Error { get; }

    /// <summary>
    /// 原始响应内容，便于排查问题。
    /// </summary>
    public string? ResponseBody { get; }

    internal static BmobException FromResponse(HttpStatusCode statusCode, string body)
    {
        var code = (int)statusCode;
        string? error = null;

        if (!string.IsNullOrWhiteSpace(body))
        {
            try
            {
                using var document = JsonDocument.Parse(body);
                if (document.RootElement.ValueKind == JsonValueKind.Object)
                {
                    if (document.RootElement.TryGetProperty("code", out var codeElement))
                    {
                        if (codeElement.ValueKind == JsonValueKind.Number && codeElement.TryGetInt32(out var parsedCode))
                        {
                            code = parsedCode;
                        }
                        else if (codeElement.ValueKind == JsonValueKind.String && int.TryParse(codeElement.GetString(), out parsedCode))
                        {
                            code = parsedCode;
                        }
                    }

                    if (document.RootElement.TryGetProperty("error", out var errorElement))
                    {
                        error = errorElement.ValueKind == JsonValueKind.String ? errorElement.GetString() : errorElement.GetRawText();
                    }
                }
            }
            catch (JsonException)
            {
                // 响应体不是 JSON，保留 HTTP 状态码作为错误码。
            }
        }

        var message = error is null
            ? $"Bmob 请求失败：HTTP {(int)statusCode} ({statusCode})，业务码 {code}。"
            : $"Bmob 请求失败：HTTP {(int)statusCode}，业务码 {code}，{error}";

        return new BmobException(code, statusCode, error, body, message);
    }
}

/// <summary>
/// 常用 Bmob 业务错误码。
/// </summary>
public static class BmobErrorCode
{
    /// <summary>对象不存在或用户名密码错误。</summary>
    public const int ObjectNotFound = 101;

    /// <summary>字段名非法或使用了保留字段。</summary>
    public const int InvalidFieldName = 105;

    /// <summary>Pointer 格式错误。</summary>
    public const int InvalidPointer = 106;

    /// <summary>JSON 格式错误或 Content-Type 不正确。</summary>
    public const int InvalidJson = 107;

    /// <summary>经纬度越界。</summary>
    public const int InvalidGeoPoint = 117;

    /// <summary>ACL 权限验证失败。</summary>
    public const int PermissionDenied = 122;

    /// <summary>用户名已被占用。</summary>
    public const int UsernameTaken = 202;

    /// <summary>手机号已被占用。</summary>
    public const int MobilePhoneTaken = 209;

    /// <summary>未登录或登录已过期。</summary>
    public const int InvalidSessionToken = 211;

    /// <summary>唯一索引重复值。</summary>
    public const int DuplicateValue = 401;

    /// <summary>QPS 超出限制。</summary>
    public const int TooManyRequests = 10076;
}
