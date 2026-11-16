using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bmob;

/// <summary>
/// Bmob 数据查询构造器，支持条件、排序、分页、include、keys、计数与子查询。
/// </summary>
public sealed class BmobQuery
{
    private readonly BmobClient _client;
    private readonly Dictionary<string, object?> _where = new(StringComparer.Ordinal);
    private readonly List<string> _orders = new();
    private readonly List<string> _includes = new();
    private readonly List<string> _keys = new();
    private int? _limit;
    private int? _skip;

    internal BmobQuery(BmobClient client, string path, string className)
    {
        _client = client;
        Path = path;
        ClassName = className;
    }

    internal string Path { get; }

    internal string ClassName { get; }

    internal const int DefaultPageSize = 100;

    /// <summary>
    /// Bmob 单次查询返回条数上限。
    /// </summary>
    public const int MaxPageSize = 1000;

    internal IReadOnlyDictionary<string, object?> WhereClause => _where;

    /// <summary>
    /// 字段等于指定值。
    /// </summary>
    public BmobQuery WhereEqualTo(string field, object? value)
    {
        ValidateField(field);
        _where[field] = value;
        return this;
    }

    /// <summary>
    /// 字段不等于指定值。
    /// </summary>
    public BmobQuery WhereNotEqualTo(string field, object? value) => AddOperator(field, "$ne", value);

    /// <summary>
    /// 字段大于指定值。
    /// </summary>
    public BmobQuery WhereGreaterThan(string field, object? value) => AddOperator(field, "$gt", value);

    /// <summary>
    /// 字段大于等于指定值。
    /// </summary>
    public BmobQuery WhereGreaterThanOrEqualTo(string field, object? value) => AddOperator(field, "$gte", value);

    /// <summary>
    /// 字段小于指定值。
    /// </summary>
    public BmobQuery WhereLessThan(string field, object? value) => AddOperator(field, "$lt", value);

    /// <summary>
    /// 字段小于等于指定值。
    /// </summary>
    public BmobQuery WhereLessThanOrEqualTo(string field, object? value) => AddOperator(field, "$lte", value);

    /// <summary>
    /// 字段值在给定集合内。
    /// </summary>
    public BmobQuery WhereContainedIn(string field, params object?[] values) =>
        AddOperator(field, "$in", values);

    /// <summary>
    /// 字段值在给定集合内。
    /// </summary>
    public BmobQuery WhereContainedIn(string field, IEnumerable<object?> values) =>
        AddOperator(field, "$in", values.ToArray());

    /// <summary>
    /// 字段值不在给定集合内。
    /// </summary>
    public BmobQuery WhereNotContainedIn(string field, params object?[] values) =>
        AddOperator(field, "$nin", values);

    /// <summary>
    /// 字段值不在给定集合内。
    /// </summary>
    public BmobQuery WhereNotContainedIn(string field, IEnumerable<object?> values) =>
        AddOperator(field, "$nin", values.ToArray());

    /// <summary>
    /// 字段是否存在。
    /// </summary>
    public BmobQuery WhereExists(string field, bool exists = true) => AddOperator(field, "$exists", exists);

    /// <summary>
    /// 数组字段包含所有给定值。
    /// </summary>
    public BmobQuery WhereContainsAll(string field, params object?[] values) =>
        AddOperator(field, "$all", values);

    /// <summary>
    /// 使用正则表达式匹配字段（Bmob 付费版功能）。
    /// </summary>
    public BmobQuery WhereMatches(string field, string pattern) => AddOperator(field, "$regex", pattern);

    /// <summary>
    /// 字段以指定前缀开头（转换为正则表达式）。
    /// </summary>
    public BmobQuery WhereStartsWith(string field, string prefix)
    {
        ArgumentNullException.ThrowIfNull(prefix);
        return WhereMatches(field, "^" + Regex.Escape(prefix));
    }

    /// <summary>
    /// 字段以指定内容结尾（转换为正则表达式）。
    /// </summary>
    public BmobQuery WhereEndsWith(string field, string suffix)
    {
        ArgumentNullException.ThrowIfNull(suffix);
        return WhereMatches(field, Regex.Escape(suffix) + "$");
    }

    /// <summary>
    /// 字段包含指定子串（转换为正则表达式）。
    /// </summary>
    public BmobQuery WhereContains(string field, string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return WhereMatches(field, Regex.Escape(value));
    }

    /// <summary>
    /// 地理位置附近查询，默认半径 100 公里。
    /// </summary>
    public BmobQuery WhereNear(string field, BmobGeoPoint point, double? maxKilometers = null)
    {
        ValidateField(field);
        ArgumentNullException.ThrowIfNull(point);

        var condition = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["$nearSphere"] = point,
        };

        if (maxKilometers.HasValue)
        {
            condition["$maxDistanceInKilometers"] = maxKilometers.Value;
        }

        _where[field] = condition;
        return this;
    }

    /// <summary>
    /// 地理位置矩形范围查询。
    /// </summary>
    public BmobQuery WhereWithinBox(string field, BmobGeoPoint southWest, BmobGeoPoint northEast)
    {
        ValidateField(field);
        ArgumentNullException.ThrowIfNull(southWest);
        ArgumentNullException.ThrowIfNull(northEast);

        _where[field] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["$within"] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["$box"] = new object?[] { southWest, northEast },
            },
        };

        return this;
    }

    /// <summary>
    /// 子查询：字段值匹配另一个查询的结果。
    /// </summary>
    public BmobQuery WhereInQuery(string field, BmobQuery subQuery)
    {
        ArgumentNullException.ThrowIfNull(subQuery);
        return AddOperator(field, "$inQuery", subQuery.ToSubQueryDescriptor());
    }

    /// <summary>
    /// 子查询：字段值不匹配另一个查询的结果。
    /// </summary>
    public BmobQuery WhereNotInQuery(string field, BmobQuery subQuery)
    {
        ArgumentNullException.ThrowIfNull(subQuery);
        return AddOperator(field, "$notInQuery", subQuery.ToSubQueryDescriptor());
    }

    /// <summary>
    /// 查询指定对象关联（Relation）的多条记录。
    /// </summary>
    public BmobQuery WhereRelatedTo(BmobObject parent, string relationKey)
    {
        ArgumentNullException.ThrowIfNull(parent);
        ArgumentException.ThrowIfNullOrWhiteSpace(relationKey);

        _where["$relatedTo"] = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["object"] = new BmobPointer(parent),
            ["key"] = relationKey,
        };

        return this;
    }

    /// <summary>
    /// 将当前条件与给定查询组合为 <c>$or</c> 查询。
    /// </summary>
    public BmobQuery Or(params BmobQuery[] queries) => Combine("$or", queries);

    /// <summary>
    /// 将当前条件与给定查询组合为 <c>$and</c> 查询。
    /// </summary>
    public BmobQuery And(params BmobQuery[] queries) => Combine("$and", queries);

    /// <summary>
    /// 展开 Pointer 字段。
    /// </summary>
    public BmobQuery Include(params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        _includes.AddRange(keys.Where(static key => !string.IsNullOrWhiteSpace(key)));
        return this;
    }

    /// <summary>
    /// 只返回指定字段（objectId、createdAt、updatedAt 始终返回）。
    /// </summary>
    public BmobQuery Select(params string[] keys)
    {
        ArgumentNullException.ThrowIfNull(keys);
        _keys.AddRange(keys.Where(static key => !string.IsNullOrWhiteSpace(key)));
        return this;
    }

    /// <summary>
    /// 按字段升序排序。
    /// </summary>
    public BmobQuery OrderBy(string field)
    {
        ValidateField(field);
        _orders.Add(field);
        return this;
    }

    /// <summary>
    /// 按字段降序排序。
    /// </summary>
    public BmobQuery OrderByDescending(string field)
    {
        ValidateField(field);
        _orders.Add("-" + field);
        return this;
    }

    /// <summary>
    /// 限制返回条数（Bmob 上限通常为 1000）。
    /// </summary>
    public BmobQuery Limit(int limit)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(limit);
        _limit = limit;
        return this;
    }

    /// <summary>
    /// 跳过指定条数。
    /// </summary>
    public BmobQuery Skip(int skip)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(skip);
        _skip = skip;
        return this;
    }

    /// <summary>
    /// 分页查询（页码从 1 开始）。
    /// </summary>
    public BmobQuery Page(int pageNumber, int pageSize)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageNumber, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        _limit = pageSize;
        _skip = (pageNumber - 1) * pageSize;
        return this;
    }

    /// <summary>
    /// 执行查询。
    /// </summary>
    public async Task<BmobQueryResult> FindAsync(CancellationToken cancellationToken = default)
    {
        var root = await _client
            .SendAsync(HttpMethod.Get, Path, BuildParameters(), null, null, cancellationToken)
            .ConfigureAwait(false);

        return BmobQueryResult.FromJson(root, ClassName);
    }

    /// <summary>
    /// 执行查询并映射为强类型模型。
    /// </summary>
    public async Task<BmobQueryResult<T>> FindAsync<T>(CancellationToken cancellationToken = default)
    {
        var result = await FindAsync(cancellationToken).ConfigureAwait(false);
        var items = new List<T>(result.Results.Count);

        foreach (var item in result.Results)
        {
            var mapped = item.ToObject<T>();
            if (mapped is not null)
            {
                items.Add(mapped);
            }
        }

        return new BmobQueryResult<T>(items, result.Count);
    }

    /// <summary>
    /// 返回第一条结果，没有结果时返回 <see langword="null"/>。
    /// </summary>
    public async Task<BmobObject?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var originalLimit = _limit;
        _limit = 1;

        try
        {
            var result = await FindAsync(cancellationToken).ConfigureAwait(false);
            return result.Results.Count > 0 ? result.Results[0] : null;
        }
        finally
        {
            _limit = originalLimit;
        }
    }

    /// <summary>
    /// 自动分页遍历所有符合条件的记录。
    /// 每页大小取 <c>Limit</c> 设置值（未设置时为 100），自动递增 <c>skip</c> 直到取完或达到 <paramref name="maxRecords"/>。
    /// </summary>
    /// <param name="maxRecords">最多读取的记录数，防止意外拉取过量数据。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<IReadOnlyList<BmobObject>> FindAllAsync(
        int maxRecords = 10000,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxRecords, 1);

        var pageSize = Math.Max(_limit ?? DefaultPageSize, 1);
        var results = new List<BmobObject>();
        var skip = _skip ?? 0;

        while (results.Count < maxRecords)
        {
            var size = Math.Min(pageSize, maxRecords - results.Count);

            var root = await _client
                .SendAsync(
                    HttpMethod.Get,
                    Path,
                    BuildParameters(limitOverride: size, skipOverride: skip),
                    null,
                    null,
                    cancellationToken)
                .ConfigureAwait(false);

            var page = BmobQueryResult.FromJson(root, ClassName);
            results.AddRange(page.Results);

            if (page.Results.Count < size)
            {
                break;
            }

            skip += page.Results.Count;
        }

        return results;
    }

    /// <summary>
    /// 自动分页遍历所有符合条件的记录并映射为强类型模型。
    /// </summary>
    public async Task<IReadOnlyList<T>> FindAllAsync<T>(
        int maxRecords = 10000,
        CancellationToken cancellationToken = default)
    {
        var items = await FindAllAsync(maxRecords, cancellationToken).ConfigureAwait(false);
        var mapped = new List<T>(items.Count);

        foreach (var item in items)
        {
            var model = item.ToObject<T>();
            if (model is not null)
            {
                mapped.Add(model);
            }
        }

        return mapped;
    }

    /// <summary>
    /// 以 <see cref="IAsyncEnumerable{T}"/> 流式遍历所有符合条件的记录，逐页拉取、逐条产出，适合处理大量数据。
    /// </summary>
    /// <param name="pageSize">每页记录数（1-1000）。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async IAsyncEnumerable<BmobObject> AsAsyncEnumerable(
        int pageSize = DefaultPageSize,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(pageSize, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(pageSize, MaxPageSize);

        var skip = _skip ?? 0;

        while (true)
        {
            var root = await _client
                .SendAsync(
                    HttpMethod.Get,
                    Path,
                    BuildParameters(limitOverride: pageSize, skipOverride: skip),
                    null,
                    null,
                    cancellationToken)
                .ConfigureAwait(false);

            var page = BmobQueryResult.FromJson(root, ClassName);

            foreach (var item in page.Results)
            {
                yield return item;
            }

            if (page.Results.Count < pageSize)
            {
                yield break;
            }

            skip += page.Results.Count;
        }
    }

    /// <summary>
    /// 返回第一条结果并映射为强类型模型，没有结果时返回 <see langword="null"/>。
    /// </summary>
    public async Task<T?> FirstOrDefaultAsync<T>(CancellationToken cancellationToken = default)
    {
        var originalLimit = _limit;
        _limit = 1;

        try
        {
            var result = await FindAsync<T>(cancellationToken).ConfigureAwait(false);
            return result.Results.Count > 0 ? result.Results[0] : default;
        }
        finally
        {
            _limit = originalLimit;
        }
    }

    /// <summary>
    /// 统计符合条件的记录数。
    /// </summary>
    public async Task<int> CountAsync(CancellationToken cancellationToken = default)
    {
        var parameters = BuildParameters(count: true, countOnly: true);
        var root = await _client
            .SendAsync(HttpMethod.Get, Path, parameters, null, null, cancellationToken)
            .ConfigureAwait(false);

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("count", out var countElement)
            && countElement.TryGetInt32(out var count))
        {
            return count;
        }

        return BmobQueryResult.FromJson(root, ClassName).Results.Count;
    }

    internal List<KeyValuePair<string, string?>> BuildParameters(
        bool count = false,
        bool countOnly = false,
        int? limitOverride = null,
        int? skipOverride = null)
    {
        var parameters = new List<KeyValuePair<string, string?>>();

        if (_where.Count > 0)
        {
            parameters.Add(new KeyValuePair<string, string?>("where", BmobJson.SerializeToString(_where)));
        }

        if (_orders.Count > 0)
        {
            parameters.Add(new KeyValuePair<string, string?>("order", string.Join(",", _orders)));
        }

        if (_includes.Count > 0)
        {
            parameters.Add(new KeyValuePair<string, string?>("include", string.Join(",", _includes)));
        }

        if (_keys.Count > 0)
        {
            parameters.Add(new KeyValuePair<string, string?>("keys", string.Join(",", _keys)));
        }

        var limit = countOnly ? 0 : (limitOverride ?? _limit);
        if (limit.HasValue)
        {
            parameters.Add(new KeyValuePair<string, string?>("limit", limit.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        var skip = skipOverride ?? _skip;
        if (skip.HasValue)
        {
            parameters.Add(new KeyValuePair<string, string?>("skip", skip.Value.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        }

        if (count || countOnly)
        {
            parameters.Add(new KeyValuePair<string, string?>("count", "1"));
        }

        return parameters;
    }

    internal Dictionary<string, object?> ToSubQueryDescriptor() => new(StringComparer.Ordinal)
    {
        ["className"] = ClassName,
        ["where"] = _where,
    };

    private BmobQuery AddOperator(string field, string operation, object? value)
    {
        ValidateField(field);

        if (_where.TryGetValue(field, out var existing)
            && existing is IDictionary<string, object?> conditions
            && !conditions.ContainsKey("__type"))
        {
            conditions[operation] = value;
        }
        else
        {
            _where[field] = new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                [operation] = value,
            };
        }

        return this;
    }

    private BmobQuery Combine(string operation, BmobQuery[] queries)
    {
        ArgumentNullException.ThrowIfNull(queries);

        var items = new List<object?>();

        if (_where.Count > 0)
        {
            items.Add(new Dictionary<string, object?>(_where, StringComparer.Ordinal));
        }

        foreach (var query in queries)
        {
            if (query is null)
            {
                continue;
            }

            if (query._where.Count > 0)
            {
                items.Add(new Dictionary<string, object?>(query._where, StringComparer.Ordinal));
            }
        }

        if (items.Count == 0)
        {
            throw new InvalidOperationException("组合查询至少需要一个查询条件。");
        }

        _where.Clear();
        _where[operation] = items;
        return this;
    }

    private static void ValidateField(string field) => ArgumentException.ThrowIfNullOrWhiteSpace(field);
}
