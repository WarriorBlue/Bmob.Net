namespace Bmob;

/// <summary>
/// 推送消息模型。
/// </summary>
public sealed class BmobPushMessage
{
    /// <summary>
    /// 推送内容（必填）。
    /// </summary>
    public string? Alert { get; set; }

    /// <summary>
    /// 通知标题（Android）。
    /// </summary>
    public string? Title { get; set; }

    /// <summary>
    /// 提示音，<c>default</c> 表示系统默认。
    /// </summary>
    public string? Sound { get; set; }

    /// <summary>
    /// 角标数字（iOS）。
    /// </summary>
    public int? Badge { get; set; }

    /// <summary>
    /// 自定义推送数据，会与 alert/title 等合并到 <c>data</c> 中。
    /// </summary>
    public IDictionary<string, object?>? Data { get; set; }

    /// <summary>
    /// 推送目标条件，例如 <c>client.Push.Target().WhereEqualTo("deviceType", "ios")</c>。
    /// </summary>
    public BmobQuery? Where { get; set; }

    /// <summary>
    /// 定时推送时间（UTC）。
    /// </summary>
    public DateTimeOffset? PushTime { get; set; }

    /// <summary>
    /// 消息有效期（秒）。
    /// </summary>
    public int? ExpirationInterval { get; set; }

    internal Dictionary<string, object?> ToPayload()
    {
        var data = new Dictionary<string, object?>(StringComparer.Ordinal);

        if (!string.IsNullOrWhiteSpace(Alert))
        {
            data["alert"] = Alert;
        }

        if (!string.IsNullOrWhiteSpace(Title))
        {
            data["title"] = Title;
        }

        if (!string.IsNullOrWhiteSpace(Sound))
        {
            data["sound"] = Sound;
        }

        if (Badge.HasValue)
        {
            data["badge"] = Badge.Value;
        }

        if (Data is not null)
        {
            foreach (var (key, value) in Data)
            {
                data[key] = value;
            }
        }

        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["data"] = data,
        };

        if (Where is not null && Where.WhereClause.Count > 0)
        {
            payload["where"] = new Dictionary<string, object?>(Where.WhereClause, StringComparer.Ordinal);
        }

        if (PushTime.HasValue)
        {
            payload["push_time"] = PushTime.Value;
        }

        if (ExpirationInterval.HasValue)
        {
            payload["expiration_interval"] = ExpirationInterval.Value;
        }

        return payload;
    }
}
