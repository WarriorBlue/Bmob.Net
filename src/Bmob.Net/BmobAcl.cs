using System.Text.Json;

namespace Bmob;

/// <summary>
/// Bmob 访问控制列表（ACL）。键可以是用户 objectId、<c>*</c>（所有用户）或 <c>role:角色名</c>。
/// </summary>
public sealed class BmobAcl : IBmobValue
{
    private readonly Dictionary<string, BmobAclEntry> _entries = new(StringComparer.Ordinal);

    /// <summary>
    /// 所有 ACL 条目。
    /// </summary>
    public IReadOnlyDictionary<string, BmobAclEntry> Entries => _entries;

    /// <summary>
    /// 允许所有用户读取。
    /// </summary>
    public BmobAcl AllowPublicRead() => Set("*", read: true, write: null);

    /// <summary>
    /// 允许所有用户读写。
    /// </summary>
    public BmobAcl AllowPublicWrite() => Set("*", read: null, write: true);

    /// <summary>
    /// 允许指定用户读取/写入。
    /// </summary>
    /// <param name="userId">用户 objectId。</param>
    /// <param name="read">是否允许读取。</param>
    /// <param name="write">是否允许写入。</param>
    public BmobAcl AllowUser(string userId, bool read = true, bool write = false) =>
        Set(userId, read, write);

    /// <summary>
    /// 允许指定角色读取/写入。
    /// </summary>
    /// <param name="roleName">角色名。</param>
    /// <param name="read">是否允许读取。</param>
    /// <param name="write">是否允许写入。</param>
    public BmobAcl AllowRole(string roleName, bool read = true, bool write = false) =>
        Set("role:" + roleName, read, write);

    /// <summary>
    /// 设置任意键的权限。
    /// </summary>
    /// <param name="key">用户 objectId、<c>*</c> 或 <c>role:角色名</c>。</param>
    /// <param name="read">读取权限，<see langword="null"/> 表示不输出该字段。</param>
    /// <param name="write">写入权限，<see langword="null"/> 表示不输出该字段。</param>
    public BmobAcl Set(string key, bool? read, bool? write)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (!_entries.TryGetValue(key, out var entry))
        {
            entry = new BmobAclEntry();
            _entries[key] = entry;
        }

        if (read.HasValue)
        {
            entry.Read = read;
        }

        if (write.HasValue)
        {
            entry.Write = write;
        }

        return this;
    }

    /// <summary>
    /// 解析 ACL JSON。
    /// </summary>
    public static BmobAcl Parse(JsonElement element)
    {
        var acl = new BmobAcl();
        if (element.ValueKind != JsonValueKind.Object)
        {
            return acl;
        }

        foreach (var property in element.EnumerateObject())
        {
            var entry = new BmobAclEntry();
            if (property.Value.ValueKind == JsonValueKind.Object)
            {
                if (property.Value.TryGetProperty("read", out var read))
                {
                    entry.Read = read.ValueKind == JsonValueKind.True;
                }

                if (property.Value.TryGetProperty("write", out var write))
                {
                    entry.Write = write.ValueKind == JsonValueKind.True;
                }
            }

            acl._entries[property.Name] = entry;
        }

        return acl;
    }

    /// <inheritdoc />
    public void WriteTo(Utf8JsonWriter writer)
    {
        writer.WriteStartObject();
        foreach (var (key, entry) in _entries)
        {
            writer.WritePropertyName(key);
            writer.WriteStartObject();
            if (entry.Read.HasValue)
            {
                writer.WriteBoolean("read", entry.Read.Value);
            }

            if (entry.Write.HasValue)
            {
                writer.WriteBoolean("write", entry.Write.Value);
            }

            writer.WriteEndObject();
        }

        writer.WriteEndObject();
    }

    /// <inheritdoc />
    public override string ToString() => string.Join(", ", _entries.Keys);
}

/// <summary>
/// 单条 ACL 权限。
/// </summary>
public sealed class BmobAclEntry
{
    /// <summary>
    /// 是否允许读取。
    /// </summary>
    public bool? Read { get; set; }

    /// <summary>
    /// 是否允许写入。
    /// </summary>
    public bool? Write { get; set; }
}
