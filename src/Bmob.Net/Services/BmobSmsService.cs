namespace Bmob.Services;

/// <summary>
/// 短信验证码服务，封装 <c>/1/requestSmsCode</c> 与 <c>/1/verifySmsCode</c>。
/// </summary>
public sealed class BmobSmsService
{
    private readonly BmobClient _client;

    internal BmobSmsService(BmobClient client)
    {
        _client = client;
    }

    /// <summary>
    /// 请求短信验证码。
    /// </summary>
    /// <param name="mobilePhoneNumber">手机号。</param>
    /// <param name="template">短信模板名称（需在 Bmob 后台创建）。</param>
    /// <param name="sign">短信签名。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task RequestCodeAsync(
        string mobilePhoneNumber,
        string? template = null,
        string? sign = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mobilePhoneNumber);

        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["mobilePhoneNumber"] = mobilePhoneNumber,
        };

        if (!string.IsNullOrWhiteSpace(template))
        {
            body["template"] = template;
        }

        if (!string.IsNullOrWhiteSpace(sign))
        {
            body["sign"] = sign;
        }

        await _client
            .SendAsync(HttpMethod.Post, "/1/requestSmsCode", null, body, null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 校验短信验证码。验证成功后验证码立即失效。
    /// </summary>
    public async Task VerifyCodeAsync(
        string mobilePhoneNumber,
        string smsCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mobilePhoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(smsCode);

        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["mobilePhoneNumber"] = mobilePhoneNumber,
            ["smsCode"] = smsCode,
        };

        await _client
            .SendAsync(HttpMethod.Post, "/1/verifySmsCode", null, body, null, cancellationToken)
            .ConfigureAwait(false);
    }
}
