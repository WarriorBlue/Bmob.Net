using System.Text.Json;

namespace Bmob.Services;

/// <summary>
/// 批量操作服务，封装 <c>/1/batch</c> 接口。
/// </summary>
public sealed class BmobBatchService
{
    /// <summary>
    /// Bmob 单次批量操作的最大子请求数。
    /// </summary>
    public const int MaxRequests = 50;

    private readonly BmobClient _client;

    internal BmobBatchService(BmobClient client)
    {
        _client = client;
    }

    /// <summary>
    /// 执行批量操作。子请求独立成功/失败，不会回滚。
    /// </summary>
    public async Task<IReadOnlyList<BmobBatchResult>> ExecuteAsync(
        BmobBatch batch,
        BmobRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);

        if (batch.Requests.Count == 0)
        {
            throw new InvalidOperationException("批量操作至少需要一条子请求。");
        }

        if (batch.Requests.Count > MaxRequests)
        {
            throw new InvalidOperationException($"Bmob 批量操作单次最多 {MaxRequests} 条子请求。");
        }

        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["requests"] = batch.Requests.Select(static request => request.ToPayload()).ToList(),
        };

        var root = await _client
            .SendAsync(HttpMethod.Post, BmobPaths.Batch, null, body, options, cancellationToken)
            .ConfigureAwait(false);

        var results = new List<BmobBatchResult>();

        if (root.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in root.EnumerateArray())
            {
                results.Add(BmobBatchResult.FromJson(item));
            }
        }

        return results;
    }
}
