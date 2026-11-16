using System.Text.Json;

namespace Bmob;

/// <summary>
/// 查询结果。
/// </summary>
public sealed class BmobQueryResult
{
    /// <summary>
    /// 创建查询结果。
    /// </summary>
    public BmobQueryResult(IReadOnlyList<BmobObject> results, int? count = null)
    {
        Results = results;
        Count = count;
    }

    /// <summary>
    /// 结果集。
    /// </summary>
    public IReadOnlyList<BmobObject> Results { get; }

    /// <summary>
    /// 符合条件的总记录数（仅当请求包含 count 参数时返回）。
    /// </summary>
    public int? Count { get; }

    internal static BmobQueryResult FromJson(JsonElement root, string className)
    {
        var items = new List<BmobObject>();
        int? count = null;

        JsonElement results = default;

        if (root.ValueKind == JsonValueKind.Array)
        {
            results = root;
        }
        else if (root.ValueKind == JsonValueKind.Object)
        {
            if (root.TryGetProperty("results", out var resultsElement))
            {
                results = resultsElement;
            }

            if (root.TryGetProperty("count", out var countElement) && countElement.TryGetInt32(out var parsedCount))
            {
                count = parsedCount;
            }
        }

        if (results.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in results.EnumerateArray())
            {
                if (item.ValueKind == JsonValueKind.Object)
                {
                    items.Add(BmobObject.FromJson(item, className));
                }
            }
        }

        return new BmobQueryResult(items, count);
    }
}

/// <summary>
/// 强类型查询结果。
/// </summary>
/// <typeparam name="T">结果元素类型。</typeparam>
public sealed class BmobQueryResult<T>
{
    /// <summary>
    /// 创建强类型查询结果。
    /// </summary>
    public BmobQueryResult(IReadOnlyList<T> results, int? count = null)
    {
        Results = results;
        Count = count;
    }

    /// <summary>
    /// 结果集。
    /// </summary>
    public IReadOnlyList<T> Results { get; }

    /// <summary>
    /// 符合条件的总记录数（仅当请求包含 count 参数时返回）。
    /// </summary>
    public int? Count { get; }
}
