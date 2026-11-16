using System.Text.Json;

namespace Bmob;

/// <summary>
/// 批量操作集合（单次最多 50 条子请求）。
/// </summary>
public sealed class BmobBatch
{
    private readonly List<BmobBatchRequest> _requests = new();

    /// <summary>
    /// 已加入的子请求。
    /// </summary>
    public IReadOnlyList<BmobBatchRequest> Requests => _requests;

    /// <summary>
    /// 批量新增数据。
    /// </summary>
    public BmobBatch Create(BmobObject data)
    {
        ArgumentNullException.ThrowIfNull(data);

        _requests.Add(new BmobBatchRequest
        {
            Method = "POST",
            Path = BmobPaths.Class(data.ClassName),
            Body = data.CreatePayload(),
        });

        return this;
    }

    /// <summary>
    /// 批量更新数据。
    /// </summary>
    public BmobBatch Update(BmobObject data, IEnumerable<string>? fields = null)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (string.IsNullOrEmpty(data.ObjectId))
        {
            throw new InvalidOperationException("批量更新需要设置 ObjectId。");
        }

        _requests.Add(new BmobBatchRequest
        {
            Method = "PUT",
            Path = BmobPaths.ClassRecord(data.ClassName, data.ObjectId),
            Body = data.UpdatePayload(fields),
        });

        return this;
    }

    /// <summary>
    /// 批量删除数据。
    /// </summary>
    public BmobBatch Delete(string tableName, string objectId)
    {
        _requests.Add(new BmobBatchRequest
        {
            Method = "DELETE",
            Path = BmobPaths.ClassRecord(tableName, objectId),
        });

        return this;
    }

    /// <summary>
    /// 添加自定义子请求。
    /// </summary>
    public BmobBatch Add(BmobBatchRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _requests.Add(request);
        return this;
    }
}

/// <summary>
/// 单条批量子请求。
/// </summary>
public sealed class BmobBatchRequest
{
    /// <summary>
    /// HTTP 方法（POST / PUT / DELETE）。
    /// </summary>
    public string Method { get; set; } = "POST";

    /// <summary>
    /// 以 <c>/1/</c> 开头的相对路径。
    /// </summary>
    public string Path { get; set; } = string.Empty;

    /// <summary>
    /// 请求体。
    /// </summary>
    public object? Body { get; set; }

    /// <summary>
    /// 子请求使用的 Session Token（可选）。
    /// </summary>
    public string? SessionToken { get; set; }

    internal Dictionary<string, object?> ToPayload()
    {
        var payload = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["method"] = Method,
            ["path"] = Path,
        };

        if (Body is not null)
        {
            payload["body"] = Body;
        }

        if (!string.IsNullOrEmpty(SessionToken))
        {
            payload["token"] = SessionToken;
        }

        return payload;
    }
}

/// <summary>
/// 单条批量子请求的执行结果。
/// </summary>
public sealed class BmobBatchResult
{
    private BmobBatchResult(bool isSuccess, JsonElement? data, int? errorCode, string? errorMessage)
    {
        IsSuccess = isSuccess;
        Data = data;
        ErrorCode = errorCode;
        ErrorMessage = errorMessage;
    }

    /// <summary>
    /// 是否执行成功。
    /// </summary>
    public bool IsSuccess { get; }

    /// <summary>
    /// 成功时服务端返回的数据（含 objectId / createdAt / updatedAt 等）。
    /// </summary>
    public JsonElement? Data { get; }

    /// <summary>
    /// 失败时的业务错误码。
    /// </summary>
    public int? ErrorCode { get; }

    /// <summary>
    /// 失败时的错误描述。
    /// </summary>
    public string? ErrorMessage { get; }

    internal static BmobBatchResult FromJson(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            if (element.TryGetProperty("success", out var success))
            {
                return new BmobBatchResult(true, success.Clone(), null, null);
            }

            if (element.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                int? code = error.TryGetProperty("code", out var codeElement) && codeElement.TryGetInt32(out var parsedCode)
                    ? parsedCode
                    : null;

                var message = error.TryGetProperty("error", out var messageElement) ? messageElement.GetString() : null;
                return new BmobBatchResult(false, null, code, message);
            }
        }

        return new BmobBatchResult(false, null, null, element.GetRawText());
    }
}
