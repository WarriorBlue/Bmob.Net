namespace Bmob.Services;

/// <summary>
/// 推送服务，封装 <c>/1/push</c> 接口。调用方必须配置 Master Key。
/// </summary>
public sealed class BmobPushService
{
    private readonly BmobClient _client;

    internal BmobPushService(BmobClient client)
    {
        _client = client;
    }

    /// <summary>
    /// 创建推送目标查询（针对 <c>_Installation</c> 表）。
    /// </summary>
    public BmobQuery Target() => new(_client, "/1/installations", "_Installation");

    /// <summary>
    /// 发送推送消息。
    /// </summary>
    public async Task SendAsync(
        BmobPushMessage message,
        BmobRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);

        if (string.IsNullOrWhiteSpace(message.Alert) && message.Data is null)
        {
            throw new InvalidOperationException("推送内容不能为空，请设置 Alert 或 Data。");
        }

        await _client
            .SendAsync(
                HttpMethod.Post,
                "/1/push",
                null,
                message.ToPayload(),
                options ?? BmobRequestOptions.Master,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 发送一条文本推送。
    /// </summary>
    public Task SendAsync(
        string alert,
        BmobQuery? where = null,
        CancellationToken cancellationToken = default) =>
        SendAsync(
            new BmobPushMessage
            {
                Alert = alert,
                Where = where,
            },
            null,
            cancellationToken);
}
