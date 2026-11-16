using System.Text.Json;

namespace Bmob.Services;

/// <summary>
/// BQL（类 SQL 查询语言）服务，封装 <c>/1/cloudQuery</c> 接口。
/// </summary>
public sealed class BmobBqlService
{
    private readonly BmobClient _client;

    internal BmobBqlService(BmobClient client)
    {
        _client = client;
    }

    /// <summary>
    /// 执行 BQL 查询并返回原始 JSON。
    /// </summary>
    /// <param name="bql">BQL 语句，例如 <c>select * from GameScore where score &gt; ? limit 0,100</c>。</param>
    /// <param name="values">与 BQL 中 <c>?</c> 占位符按顺序对应的参数。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<JsonElement> QueryAsync(
        string bql,
        IEnumerable<object?>? values = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(bql);

        var parameters = new List<KeyValuePair<string, string?>>(2)
        {
            new("bql", bql),
        };

        if (values is not null)
        {
            parameters.Add(new KeyValuePair<string, string?>("values", BmobJson.SerializeToString(values)));
        }

        return await _client
            .SendAsync(HttpMethod.Get, BmobPaths.CloudQuery, parameters, null, null, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 执行 BQL 查询并映射为强类型结果。
    /// </summary>
    /// <param name="bql">BQL 语句。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public Task<IReadOnlyList<T>> FindAsync<T>(
        string bql,
        CancellationToken cancellationToken = default) =>
        FindCoreAsync<T>(bql, null, cancellationToken);

    /// <summary>
    /// 执行带参数的 BQL 查询并映射为强类型结果。
    /// </summary>
    /// <param name="bql">BQL 语句，例如 <c>select * from Game where score &gt; ?</c>。</param>
    /// <param name="values">与 BQL 中 <c>?</c> 占位符按顺序对应的参数。</param>
    public Task<IReadOnlyList<T>> FindAsync<T>(
        string bql,
        params object?[] values) =>
        FindCoreAsync<T>(bql, values.Length > 0 ? values : null, CancellationToken.None);

    private async Task<IReadOnlyList<T>> FindCoreAsync<T>(
        string bql,
        IEnumerable<object?>? values,
        CancellationToken cancellationToken)
    {
        var root = await QueryAsync(bql, values, cancellationToken).ConfigureAwait(false);

        JsonElement results = default;

        if (root.ValueKind == JsonValueKind.Array)
        {
            results = root;
        }
        else if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("results", out var resultsElement))
        {
            results = resultsElement;
        }

        var items = new List<T>();

        if (results.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in results.EnumerateArray())
            {
                var value = item.Deserialize<T>(BmobJson.Options);
                if (value is not null)
                {
                    items.Add(value);
                }
            }
        }

        return items;
    }
}
