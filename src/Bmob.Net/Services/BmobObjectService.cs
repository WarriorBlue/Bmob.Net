using System.Text.Json;

namespace Bmob.Services;

/// <summary>
/// 数据对象服务，封装 <c>/1/classes</c> 相关接口。
/// </summary>
public sealed class BmobObjectService
{
    private readonly BmobClient _client;

    internal BmobObjectService(BmobClient client)
    {
        _client = client;
    }

    /// <summary>
    /// 新增一条数据，成功后回填 <see cref="BmobObject.ObjectId"/> 与 <see cref="BmobObject.CreatedAt"/>。
    /// </summary>
    public async Task<BmobObject> CreateAsync(
        BmobObject data,
        BmobRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        var root = await _client
            .SendAsync(HttpMethod.Post, BmobPaths.Class(data.ClassName), null, data.CreatePayload(), options, cancellationToken)
            .ConfigureAwait(false);

        data.ApplyResult(root);
        data.ClearDirtyFields();
        return data;
    }

    /// <summary>
    /// 使用字段字典新增一条数据。
    /// </summary>
    public Task<BmobObject> CreateAsync(
        string tableName,
        IDictionary<string, object?> fields,
        BmobRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var data = new BmobObject(tableName).SetAll(fields);
        return CreateAsync(data, options, cancellationToken);
    }

    /// <summary>
    /// 使用强类型模型新增一条数据。
    /// </summary>
    public Task<BmobObject> CreateAsync<T>(
        string tableName,
        T value,
        BmobRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var data = BmobObject.From(tableName, value);
        return CreateAsync(data, options, cancellationToken);
    }

    /// <summary>
    /// 按 objectId 查询单条数据。
    /// </summary>
    /// <param name="tableName">表名。</param>
    /// <param name="objectId">记录 objectId。</param>
    /// <param name="include">需要展开的 Pointer 字段。</param>
    /// <param name="options">请求选项。</param>
    /// <param name="cancellationToken">取消令牌。</param>
    public async Task<BmobObject> GetAsync(
        string tableName,
        string objectId,
        IEnumerable<string>? include = null,
        BmobRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var parameters = include is null
            ? null
            : new[] { new KeyValuePair<string, string?>("include", string.Join(",", include)) };

        var root = await _client
            .SendAsync(HttpMethod.Get, BmobPaths.ClassRecord(tableName, objectId), parameters, null, options, cancellationToken)
            .ConfigureAwait(false);

        if (root.ValueKind != JsonValueKind.Object)
        {
            throw new BmobException("Bmob 返回的数据对象格式不正确。");
        }

        return BmobObject.FromJson(root, tableName);
    }

    /// <summary>
    /// 按 objectId 查询单条数据并映射为强类型模型。
    /// </summary>
    public async Task<T?> GetAsAsync<T>(
        string tableName,
        string objectId,
        IEnumerable<string>? include = null,
        BmobRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        var data = await GetAsync(tableName, objectId, include, options, cancellationToken).ConfigureAwait(false);
        return data.ToObject<T>();
    }

    /// <summary>
    /// 更新数据。默认只提交被修改过的字段，也可以通过 <paramref name="fields"/> 显式指定。
    /// </summary>
    public async Task<BmobObject> UpdateAsync(
        BmobObject data,
        IEnumerable<string>? fields = null,
        BmobRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (string.IsNullOrEmpty(data.ObjectId))
        {
            throw new InvalidOperationException("更新数据前必须先设置 ObjectId。");
        }

        var payload = data.UpdatePayload(fields);

        var root = await _client
            .SendAsync(HttpMethod.Put, BmobPaths.ClassRecord(data.ClassName, data.ObjectId), null, payload, options, cancellationToken)
            .ConfigureAwait(false);

        data.ApplyResult(root);
        data.ClearDirtyFields();
        return data;
    }

    /// <summary>
    /// 使用字段字典更新数据。
    /// </summary>
    public Task<BmobObject> UpdateAsync(
        string tableName,
        string objectId,
        IDictionary<string, object?> fields,
        BmobRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(fields);

        var data = new BmobObject(tableName) { ObjectId = objectId };
        data.SetAll(fields);
        return UpdateAsync(data, null, options, cancellationToken);
    }

    /// <summary>
    /// 保存数据：<see cref="BmobObject.ObjectId"/> 为空时新增，否则更新。
    /// </summary>
    public Task<BmobObject> SaveAsync(
        BmobObject data,
        BmobRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        return string.IsNullOrEmpty(data.ObjectId)
            ? CreateAsync(data, options, cancellationToken)
            : UpdateAsync(data, null, options, cancellationToken);
    }

    /// <summary>
    /// 删除一条数据。
    /// </summary>
    public async Task DeleteAsync(
        string tableName,
        string objectId,
        BmobRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await _client
            .SendAsync(HttpMethod.Delete, BmobPaths.ClassRecord(tableName, objectId), null, null, options, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 删除一条数据并清空其 <see cref="BmobObject.ObjectId"/>。
    /// </summary>
    public async Task DeleteAsync(
        BmobObject data,
        BmobRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(data);

        if (string.IsNullOrEmpty(data.ObjectId))
        {
            throw new InvalidOperationException("删除数据前必须先设置 ObjectId。");
        }

        await DeleteAsync(data.ClassName, data.ObjectId, options, cancellationToken).ConfigureAwait(false);
        data.ObjectId = null;
    }

    /// <summary>
    /// 创建针对指定表的查询。
    /// </summary>
    public BmobQuery Query(string tableName) =>
        new(_client, BmobPaths.Class(tableName), tableName);

    /// <summary>
    /// 执行查询。
    /// </summary>
    public Task<BmobQueryResult> FindAsync(BmobQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        return query.FindAsync(cancellationToken);
    }
}
