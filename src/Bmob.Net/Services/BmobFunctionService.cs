using System.Text.Json;

namespace Bmob.Services;

/// <summary>
/// 云函数服务，封装 <c>/1/functions/{name}</c> 接口。
/// </summary>
public sealed class BmobFunctionService
{
    private readonly BmobClient _client;

    internal BmobFunctionService(BmobClient client)
    {
        _client = client;
    }

    /// <summary>
    /// 调用云函数并返回原始 JSON 结果。
    /// </summary>
    /// <param name="functionName">云函数名称。</param>
    /// <param name="parameters">参数对象，会作为请求体 JSON 发送。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<JsonElement> InvokeAsync(
        string functionName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        return await _client
            .SendAsync(
                HttpMethod.Post,
                BmobPaths.Function(functionName),
                null,
                parameters ?? new Dictionary<string, object?>(),
                null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 调用云函数并反序列化为指定类型。
    /// </summary>
    public async Task<T?> InvokeAsync<T>(
        string functionName,
        object? parameters = null,
        CancellationToken cancellationToken = default)
    {
        var root = await InvokeAsync(functionName, parameters, cancellationToken).ConfigureAwait(false);

        if (root.ValueKind is JsonValueKind.Undefined or JsonValueKind.Null)
        {
            return default;
        }

        return root.Deserialize<T>(BmobJson.Options);
    }
}
