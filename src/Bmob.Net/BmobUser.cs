using System.Text.Json;

namespace Bmob;

/// <summary>
/// Bmob 用户对象，对应 <c>_User</c> 表。
/// </summary>
public sealed class BmobUser : BmobObject
{
    /// <summary>
    /// 用户表表名。
    /// </summary>
    public const string TableName = "_User";

    /// <summary>
    /// 创建用户对象。
    /// </summary>
    public BmobUser()
        : base(TableName)
    {
    }

    internal BmobUser(BmobObject source)
        : base(source)
    {
    }

    /// <summary>
    /// 用户名。
    /// </summary>
    public string? Username
    {
        get => GetString("username");
        set => Set("username", value);
    }

    /// <summary>
    /// 密码（只写，Bmob 永不返回密码）。
    /// </summary>
    public string? Password
    {
        set
        {
            if (value is not null)
            {
                Set("password", value);
            }
        }
    }

    /// <summary>
    /// 邮箱。
    /// </summary>
    public string? Email
    {
        get => GetString("email");
        set => Set("email", value);
    }

    /// <summary>
    /// 手机号。
    /// </summary>
    public string? MobilePhoneNumber
    {
        get => GetString("mobilePhoneNumber");
        set => Set("mobilePhoneNumber", value);
    }

    /// <summary>
    /// 邮箱是否已验证。<see langword="null"/> 表示服务端未返回该字段。
    /// </summary>
    public bool? EmailVerified => GetBool("emailVerified");

    /// <summary>
    /// 手机号是否已验证。
    /// </summary>
    public bool? MobilePhoneVerified => GetBool("mobilePhoneVerified");

    /// <summary>
    /// 登录后返回的会话令牌，用于代表当前用户执行需要权限的请求。
    /// 可自行持久化，并在下次启动时通过 <see cref="BmobClient.CurrentUser"/> 恢复登录状态。
    /// </summary>
    public string? SessionToken { get; set; }

    /// <summary>
    /// 解析用户 JSON。
    /// </summary>
    internal static BmobUser Parse(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new FormatException($"无法将 JSON 值解析为 Bmob 用户：{element.GetRawText()}");
        }

        var user = new BmobUser();
        user.LoadFrom(element);
        user.SessionToken = user.GetString("sessionToken");
        user.RemoveField("sessionToken");
        return user;
    }

    /// <summary>
    /// 从普通数据对象转换为用户对象。
    /// </summary>
    internal static BmobUser FromObject(BmobObject source) => new(source);
}
