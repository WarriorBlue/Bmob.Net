using System.Text.Json;

namespace Bmob.Services;

/// <summary>
/// 用户服务，封装 <c>/1/users</c>、<c>/1/login</c> 等接口。
/// </summary>
public sealed class BmobUserService
{
    private readonly BmobClient _client;

    internal BmobUserService(BmobClient client)
    {
        _client = client;
    }

    /// <summary>
    /// 使用用户名、密码注册用户。
    /// </summary>
    public Task<BmobUser> RegisterAsync(
        string username,
        string password,
        string? email = null,
        IDictionary<string, object?>? fields = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        var user = new BmobUser
        {
            Username = username,
            Password = password,
        };

        if (!string.IsNullOrWhiteSpace(email))
        {
            user.Email = email;
        }

        if (fields is not null)
        {
            user.SetAll(fields);
        }

        return RegisterAsync(user, cancellationToken);
    }

    /// <summary>
    /// 注册一个用户对象。成功后会自动设置 <see cref="BmobClient.CurrentUser"/>。
    /// </summary>
    public async Task<BmobUser> RegisterAsync(BmobUser user, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        var root = await _client
            .SendAsync(HttpMethod.Post, BmobPaths.Users, null, user.CreatePayload(), null, cancellationToken)
            .ConfigureAwait(false);

        CompleteUser(user, root);
        _client.CurrentUser = user;
        return user;
    }

    /// <summary>
    /// 使用手机号 + 短信验证码注册或登录（验证码需先通过 <see cref="BmobSmsService"/> 获取）。
    /// </summary>
    public async Task<BmobUser> RegisterWithMobileAsync(
        string mobilePhoneNumber,
        string smsCode,
        IDictionary<string, object?>? fields = null,
        CancellationToken cancellationToken = default)
    {
        var user = new BmobUser { MobilePhoneNumber = mobilePhoneNumber };
        user.Set("smsCode", smsCode);

        if (fields is not null)
        {
            user.SetAll(fields);
        }

        return await RegisterAsync(user, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// 用户名（或邮箱、手机号）+ 密码登录。
    /// </summary>
    public Task<BmobUser> LoginAsync(string username, string password, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        return LoginCoreAsync(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["username"] = username,
            ["password"] = password,
        }, cancellationToken);
    }

    /// <summary>
    /// 手机号 + 短信验证码登录。
    /// </summary>
    public Task<BmobUser> LoginWithSmsCodeAsync(
        string mobilePhoneNumber,
        string smsCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mobilePhoneNumber);
        ArgumentException.ThrowIfNullOrWhiteSpace(smsCode);

        return LoginCoreAsync(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["mobilePhoneNumber"] = mobilePhoneNumber,
            ["smsCode"] = smsCode,
        }, cancellationToken);
    }

    /// <summary>
    /// 邮箱 + 邮件验证码登录。
    /// </summary>
    public Task<BmobUser> LoginWithEmailCodeAsync(
        string email,
        string emailCode,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);
        ArgumentException.ThrowIfNullOrWhiteSpace(emailCode);

        return LoginCoreAsync(new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["username"] = email,
            ["smsCode"] = emailCode,
        }, cancellationToken);
    }

    /// <summary>
    /// 退出登录，默认退出 <see cref="BmobClient.CurrentUser"/>。
    /// </summary>
    public async Task LogoutAsync(BmobUser? user = null, CancellationToken cancellationToken = default)
    {
        var target = user ?? _client.CurrentUser;
        if (target is null)
        {
            return;
        }

        if (string.IsNullOrEmpty(target.ObjectId))
        {
            throw new InvalidOperationException("退出登录需要用户 objectId。");
        }

        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["objectId"] = target.ObjectId,
        };

        await _client
            .SendAsync(HttpMethod.Post, BmobPaths.Logout, null, body, SessionOptions(target), cancellationToken)
            .ConfigureAwait(false);

        if (_client.CurrentUser is null || _client.CurrentUser.ObjectId == target.ObjectId)
        {
            _client.CurrentUser = null;
        }

        target.SessionToken = null;
    }

    /// <summary>
    /// 按 objectId 查询用户。
    /// </summary>
    public async Task<BmobUser> GetAsync(string objectId, CancellationToken cancellationToken = default)
    {
        var root = await _client
            .SendAsync(HttpMethod.Get, BmobPaths.User(objectId), null, null, null, cancellationToken)
            .ConfigureAwait(false);

        return BmobUser.Parse(root);
    }

    /// <summary>
    /// 获取当前登录用户的最新信息；未登录时返回 <see langword="null"/>。
    /// </summary>
    public async Task<BmobUser?> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var current = _client.CurrentUser;
        if (current?.ObjectId is null)
        {
            return null;
        }

        var root = await _client
            .SendAsync(HttpMethod.Get, BmobPaths.User(current.ObjectId), null, null, SessionOptions(current), cancellationToken)
            .ConfigureAwait(false);

        var user = BmobUser.Parse(root);
        user.SessionToken = current.SessionToken;
        _client.CurrentUser = user;
        return user;
    }

    /// <summary>
    /// 检查 Session Token 是否仍然有效。
    /// </summary>
    public async Task<bool> CheckSessionAsync(string objectId, CancellationToken cancellationToken = default)
    {
        var root = await _client
            .SendAsync(
                HttpMethod.Get,
                "/1/checkSession/" + Uri.EscapeDataString(objectId),
                null,
                null,
                BmobRequestOptions.CurrentSession,
                cancellationToken)
            .ConfigureAwait(false);

        return root.ValueKind == JsonValueKind.Object
               && root.TryGetProperty("msg", out var message)
               && string.Equals(message.GetString(), "ok", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 更新用户信息，需要用户的 Session Token（默认使用当前登录用户）。
    /// </summary>
    public async Task<BmobUser> UpdateAsync(
        BmobUser user,
        IEnumerable<string>? fields = null,
        BmobRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrEmpty(user.ObjectId))
        {
            throw new InvalidOperationException("更新用户前必须设置 ObjectId。");
        }

        var payload = user.UpdatePayload(fields);

        var root = await _client
            .SendAsync(HttpMethod.Put, BmobPaths.User(user.ObjectId), null, payload, options ?? SessionOptions(user), cancellationToken)
            .ConfigureAwait(false);

        user.ApplyResult(root);
        user.ClearDirtyFields();
        return user;
    }

    /// <summary>
    /// 删除用户，需要用户的 Session Token（或 Master Key）。
    /// </summary>
    public async Task DeleteAsync(
        BmobUser user,
        BmobRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(user);

        if (string.IsNullOrEmpty(user.ObjectId))
        {
            throw new InvalidOperationException("删除用户前必须设置 ObjectId。");
        }

        await _client
            .SendAsync(HttpMethod.Delete, BmobPaths.User(user.ObjectId), null, null, options ?? SessionOptions(user), cancellationToken)
            .ConfigureAwait(false);

        if (_client.CurrentUser?.ObjectId == user.ObjectId)
        {
            _client.CurrentUser = null;
        }
    }

    /// <summary>
    /// 使用旧密码修改密码。
    /// </summary>
    public async Task UpdatePasswordAsync(
        string objectId,
        string oldPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["oldPassword"] = oldPassword,
            ["newPassword"] = newPassword,
        };

        await _client
            .SendAsync(
                HttpMethod.Put,
                "/1/updateUserPassword/" + Uri.EscapeDataString(objectId),
                null,
                body,
                BmobRequestOptions.CurrentSession,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 通过短信验证码重置密码。
    /// </summary>
    public async Task ResetPasswordBySmsCodeAsync(
        string smsCode,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var body = new Dictionary<string, object?>(StringComparer.Ordinal)
        {
            ["password"] = newPassword,
        };

        await _client
            .SendAsync(
                HttpMethod.Put,
                "/1/resetPasswordBySmsCode/" + Uri.EscapeDataString(smsCode),
                null,
                body,
                null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// 发送重置密码邮件。
    /// </summary>
    public Task RequestPasswordResetAsync(string email, CancellationToken cancellationToken = default) =>
        PostAsync("/1/requestPasswordReset", new Dictionary<string, object?>(StringComparer.Ordinal) { ["email"] = email }, cancellationToken);

    /// <summary>
    /// 重新发送邮箱验证邮件。
    /// </summary>
    public Task RequestEmailVerifyAsync(string email, CancellationToken cancellationToken = default) =>
        PostAsync("/1/requestEmailVerify", new Dictionary<string, object?>(StringComparer.Ordinal) { ["email"] = email }, cancellationToken);

    /// <summary>
    /// 请求邮箱验证码（用于邮箱验证码登录）。
    /// </summary>
    public Task RequestEmailCodeAsync(string email, CancellationToken cancellationToken = default) =>
        PostAsync("/1/requestEmailCode", new Dictionary<string, object?>(StringComparer.Ordinal) { ["email"] = email }, cancellationToken);

    /// <summary>
    /// 创建针对 <c>_User</c> 表的查询。
    /// </summary>
    public BmobQuery Query() => new(_client, BmobPaths.Users, BmobUser.TableName);

    /// <summary>
    /// 执行用户查询并映射为用户对象。
    /// </summary>
    public async Task<IReadOnlyList<BmobUser>> FindAsync(BmobQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var result = await query.FindAsync(cancellationToken).ConfigureAwait(false);
        var users = new List<BmobUser>(result.Results.Count);

        foreach (var item in result.Results)
        {
            users.Add(BmobUser.FromObject(item));
        }

        return users;
    }

    private async Task<BmobUser> LoginCoreAsync(
        Dictionary<string, object?> body,
        CancellationToken cancellationToken)
    {
        var root = await _client
            .SendAsync(HttpMethod.Post, BmobPaths.Login, null, body, null, cancellationToken)
            .ConfigureAwait(false);

        var user = BmobUser.Parse(root);
        _client.CurrentUser = user;
        return user;
    }

    private async Task PostAsync(string path, object body, CancellationToken cancellationToken)
    {
        await _client.SendAsync(HttpMethod.Post, path, null, body, null, cancellationToken).ConfigureAwait(false);
    }

    private static void CompleteUser(BmobUser user, JsonElement root)
    {
        user.ApplyResult(root);

        if (root.ValueKind == JsonValueKind.Object
            && root.TryGetProperty("sessionToken", out var sessionToken)
            && sessionToken.ValueKind == JsonValueKind.String)
        {
            user.SessionToken = sessionToken.GetString();
        }

        user.ClearDirtyFields();
    }

    private static BmobRequestOptions SessionOptions(BmobUser user) => new()
    {
        SessionToken = user.SessionToken,
        UseCurrentSession = string.IsNullOrEmpty(user.SessionToken),
    };
}
