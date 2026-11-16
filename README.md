# Bmob .NET SDK

基于 Bmob REST API 的现代化 .NET SDK，面向 **.NET 8** 与 **.NET 10**，可直接打包发布到 NuGet。

- 零第三方依赖，仅使用 BCL（`System.Net.Http` + `System.Text.Json`）
- 全异步 API，支持 `CancellationToken`
- 覆盖数据 CRUD、条件查询、用户体系、文件上传、云函数、BQL、批量操作、短信验证码、推送
- 支持 Bmob 特殊数据类型：`Pointer`、`Date`、`File`、`GeoPoint`、`Relation`、ACL、原子操作
- 同时提供字典式动态对象与 POCO 强类型映射

## 目录

- [安装](#安装)
- [快速开始](#快速开始)
- [数据操作](#数据操作)
- [查询](#查询)
- [异步与分页](#异步与分页)
- [用户体系](#用户体系)
- [文件](#文件)
- [云函数](#云函数)
- [BQL](#bql)
- [批量操作](#批量操作)
- [短信与推送](#短信与推送)
- [强类型模型](#强类型模型)
- [自定义接口](#自定义接口)
- [错误处理](#错误处理)
- [与旧版 SDK 对照](#与旧版-cnbmob-sdk-对照)
- [构建与发布 NuGet](#构建与发布-nuget)
- [包信息](#包信息)
- [说明与限制](#说明与限制)

## 安装

```bash
dotnet add package Bmob.Net
```

或在 `PackageReference` 中引用：

```xml
<PackageReference Include="Bmob.Net" Version="1.0.0" />
```

| 目标框架 | 支持情况 |
| --- | --- |
| `net8.0` | 支持 |
| `net10.0` | 支持 |

> 本包不提供 `netstandard2.0` / .NET Framework 目标；旧平台请继续使用旧版 `Bmob-Windows.dll`。

## 快速开始

```csharp
using Bmob;

using var client = new BmobClient(new BmobClientOptions
{
    ApplicationId = "你的 Application ID",
    RestApiKey = "你的 REST API Key",
    // 新版 Bmob 应用通常需要填写控制台绑定的已备案域名：
    // BaseUrl = "https://api.你的域名.com",
});
```

> `BaseUrl` 默认值为 `https://api.bmobcloud.com`。若你的应用已绑定独立域名，请按 Bmob 控制台
> “设置 → 应用密钥/域名配置”中的地址填写，否则请求可能返回 401 或 404。

## 数据操作

### 新增

```csharp
var game = new BmobObject("Game")
    .Set("score", 1337)
    .Set("playerName", "Sean Plott")
    .Set("cheatMode", false);

await client.Objects.CreateAsync(game);

Console.WriteLine(game.ObjectId);   // 服务端生成的 objectId
Console.WriteLine(game.CreatedAt);  // UTC 时间
```

### 查询单条

```csharp
var one = await client.Objects.GetAsync("Game", game.ObjectId!);
var score = one.GetInt("score");
var player = one.GetString("playerName");
```

### 更新

```csharp
// 只提交被修改的字段
game.Set("playerName", "Bmob后端云");
await client.Objects.UpdateAsync(game);

// 原子计数器
await client.Objects.UpdateAsync(game.Increment("score", 1));

// 数组操作
await client.Objects.UpdateAsync(game.AddUnique("tags", "sdk"));

// 删除字段
await client.Objects.UpdateAsync(game.DeleteField("cheatMode"));
```

### 删除

```csharp
await client.Objects.DeleteAsync("Game", game.ObjectId!);
// 或
await client.Objects.DeleteAsync(game);
```

### ACL 与关联

```csharp
// 仅创建者与所有人可读，创建者可写
game.Acl = new BmobAcl()
    .AllowPublicRead()
    .AllowUser(user.ObjectId!, read: true, write: true);

// Pointer 关联
var post = new BmobObject("Post")
    .Set("title", "hello")
    .Set("author", new BmobPointer("_User", user.ObjectId!));

// 多对多关系
await client.Objects.UpdateAsync(post.AddRelation("likes", user));
```

## 查询

```csharp
var result = await client.Objects.Query("Game")
    .WhereGreaterThanOrEqualTo("score", 100)
    .WhereLessThan("score", 1000)
    .WhereContainedIn("playerName", new[] { "Sean Plott", "ZeroCool" })
    .OrderByDescending("createdAt")
    .Include("author")          // 展开 Pointer
    .Select("score", "playerName")
    .Page(pageNumber: 1, pageSize: 20)
    .FindAsync();

foreach (var item in result.Results)
{
    Console.WriteLine($"{item.ObjectId} {item.GetInt("score")}");
}

// 总数
var count = await client.Objects.Query("Game").WhereEqualTo("cheatMode", false).CountAsync();

// 第一条
var first = await client.Objects.Query("Game").OrderByDescending("score").FirstOrDefaultAsync();

// 强类型结果
var typed = await client.Objects.Query("Game").Limit(10).FindAsync<GameScore>();
```

支持的查询方法：

| 方法 | Bmob 操作符 |
| --- | --- |
| `WhereEqualTo` / `WhereNotEqualTo` | 相等 / `$ne` |
| `WhereGreaterThan` / `WhereGreaterThanOrEqualTo` | `$gt` / `$gte` |
| `WhereLessThan` / `WhereLessThanOrEqualTo` | `$lt` / `$lte` |
| `WhereContainedIn` / `WhereNotContainedIn` | `$in` / `$nin` |
| `WhereExists` | `$exists` |
| `WhereContainsAll` | `$all` |
| `WhereMatches` / `WhereStartsWith` / `WhereEndsWith` / `WhereContains` | `$regex` |
| `WhereNear` / `WhereWithinBox` | `$nearSphere` / `$within` |
| `WhereInQuery` / `WhereNotInQuery` | `$inQuery` / `$notInQuery` |
| `WhereRelatedTo` | `$relatedTo` |
| `Or` / `And` | `$or` / `$and` |

> `$regex` 为 Bmob 付费版功能，具体以你所用套餐为准。

## 异步与分页

```csharp
// 自动分页：自动递增 skip，绕过单次查询条数限制，遍历所有结果
var all = await client.Objects.Query("Game")
    .WhereGreaterThanOrEqualTo("score", 0)
    .FindAllAsync(maxRecords: 10000);

// 流式遍历：逐页拉取、逐条产出（IAsyncEnumerable），适合处理大量数据
await foreach (var item in client.Objects.Query("Game").AsAsyncEnumerable(pageSize: 200))
{
    Console.WriteLine(item.ObjectId);
}

// 保存：ObjectId 为空时新增，否则更新
await client.Objects.SaveAsync(data);

// 流式上传（可定位的流不额外占用内存）与下载到本地文件
var bigFile = await client.Files.UploadAsync(stream, "video.mp4", "video/mp4");
var savedPath = await client.Files.DownloadToFileAsync(bigFile.Url!, "/tmp/video.mp4");

// 所有 API 均支持取消令牌
using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
await client.Objects.GetAsync("Game", "id", cancellationToken: cts.Token);
```

SDK 内部所有 `await` 均使用 `ConfigureAwait(false)`，可在 ASP.NET Core 等无同步上下文的环境高效运行。

## 用户体系

```csharp
// 注册（成功后自动设置 client.CurrentUser）
var user = await client.Users.RegisterAsync("tom", "Bmob@123456", "tom@example.com");

// 登录
var logged = await client.Users.LoginAsync("tom", "Bmob@123456");
Console.WriteLine(logged.SessionToken);

// 手机号 + 短信验证码登录
await client.Sms.RequestCodeAsync("13800138000");
await client.Users.LoginWithSmsCodeAsync("13800138000", "123456");

// 当前用户
var current = await client.Users.GetCurrentAsync();

// 更新资料（需要 Session Token，默认使用当前登录用户）
current!.Set("nickname", "小明");
await client.Users.UpdateAsync(current);

// 修改密码 / 重置密码
await client.Users.UpdatePasswordAsync(current.ObjectId!, "old-password", "new-password");
await client.Users.RequestPasswordResetAsync("tom@example.com");

// 退出登录
await client.Users.LogoutAsync();

// 查询用户
var users = await client.Users.FindAsync(
    client.Users.Query().WhereStartsWith("username", "tom"));

// 恢复登录状态（SessionToken 由你自己持久化）
client.CurrentUser = new BmobUser
{
    ObjectId = savedObjectId,
    SessionToken = savedSessionToken,
};
```

服务端场景可使用 Master Key 绕过 ACL：

```csharp
var client = new BmobClient(new BmobClientOptions
{
    ApplicationId = "...",
    RestApiKey = "...",
    MasterKey = "...",              // 仅服务端保存
    UseMasterKeyByDefault = true,   // 所有请求默认使用 Master Key
});

// 或仅对单次请求生效
await client.Objects.DeleteAsync("Game", "id", BmobRequestOptions.Master);
```

## 文件

```csharp
// 本地文件（自动推断 MIME 类型）
var file = await client.Files.UploadAsync("C:/images/avatar.png");
Console.WriteLine(file.Url);

// 字节数组 / 流
var uploaded = await client.Files.UploadAsync(bytes, "avatar.png", "image/png");
await client.Files.UploadAsync(stream, "report.pdf");

// 关联到数据表
var profile = new BmobObject("Profile").Set("avatar", uploaded);
await client.Objects.CreateAsync(profile);

// 删除（依赖上传返回的 cdn 标识）
await client.Files.DeleteAsync(uploaded);
```

## 云函数

```csharp
// 返回原始 JSON
var raw = await client.Functions.InvokeAsync("hello", new { name = "bmob" });

// 反序列化为强类型
var result = await client.Functions.InvokeAsync<Dictionary<string, string>>("hello", new { name = "bmob" });
```

## BQL

```csharp
var rows = await client.Bql.FindAsync<GameScore>(
    "select * from Game where score > ? limit 0,20",
    100);
```

## 批量操作

```csharp
var batch = new BmobBatch()
    .Create(new BmobObject("Game").Set("score", 1))
    .Create(new BmobObject("Game").Set("score", 2))
    .Delete("Game", "old-object-id");

var results = await client.Batch.ExecuteAsync(batch);

foreach (var item in results)
{
    Console.WriteLine(item.IsSuccess
        ? $"成功：{item.Data}"
        : $"失败：{item.ErrorCode} {item.ErrorMessage}");
}
```

单次最多 50 条子请求，子请求之间不会回滚。

## 短信与推送

```csharp
await client.Sms.RequestCodeAsync("13800138000", template: "验证码模板");
await client.Sms.VerifyCodeAsync("13800138000", "123456");

// 推送需要配置 Master Key
await client.Push.SendAsync(new BmobPushMessage
{
    Alert = "Hello Bmob",
    Title = "通知",
    Where = client.Push.Target().WhereEqualTo("deviceType", "ios"),
});
```

## 强类型模型

任意 POCO 都可以直接读写，属性按 camelCase 映射到 Bmob 字段：

```csharp
public sealed class GameScore
{
    public string? ObjectId { get; set; }
    public string? PlayerName { get; set; }
    public int? Score { get; set; }
    public bool? CheatMode { get; set; }
    public DateTimeOffset? CreatedAt { get; set; }
}

var created = await client.Objects.CreateAsync("Game", new GameScore
{
    PlayerName = "Sean Plott",
    Score = 1337,
});

var model = await client.Objects.GetAsAsync<GameScore>("Game", created.ObjectId!);
```

特殊类型可直接使用 `BmobPointer`、`BmobDate`、`BmobFile`、`BmobGeoPoint`、
`BmobAcl`、`BmobOperation`，它们会被序列化为 Bmob 要求的 `__type` JSON 结构。

## 自定义接口

SDK 未封装的接口（角色、文件管理器等）可通过 `RequestAsync` 调用：

```csharp
var response = await client.RequestAsync(
    HttpMethod.Post,
    "/1/roles",
    new { name = "admin", ACL = new BmobAcl().AllowPublicRead() });
```

## 错误处理

所有失败请求都会抛出 `BmobException`，包含 HTTP 状态码、Bmob 业务错误码与原始响应：

```csharp
try
{
    await client.Objects.GetAsync("Game", "not-exists");
}
catch (BmobException ex) when (ex.Code == BmobErrorCode.ObjectNotFound)
{
    Console.WriteLine($"对象不存在：{ex.Message}");
}
```

常用错误码见 `BmobErrorCode`（101 对象不存在、105 字段非法、107 JSON 错误、122 权限不足、
202 用户名占用、211 会话失效等）。

## 与旧版 `cn.bmob` SDK 对照

| 旧版（Bmob-Windows.dll） | 本 SDK |
| --- | --- |
| `Bmob.initialize(appId, restKey)` | `new BmobClient(new BmobClientOptions { ... })` |
| `Bmob.CreateTaskAsync(obj)` | `client.Objects.CreateAsync(obj)` |
| `Bmob.FindTaskAsync(table, query)` | `client.Objects.Query(table)...FindAsync()` |
| `Bmob.GetTaskAsync(table, id)` | `client.Objects.GetAsync(table, id)` |
| `Bmob.UpdateTaskAsync(obj)` | `client.Objects.UpdateAsync(obj)` |
| `Bmob.DeleteTaskAsync(table, id)` | `client.Objects.DeleteAsync(table, id)` |
| `Bmob.LoginTaskAsync(user, pwd)` | `client.Users.LoginAsync(user, pwd)` |
| `Bmob.FileUploadTaskAsync(new BmobLocalFile(...))` | `client.Files.UploadAsync(...)` |
| `Bmob.EndpointTaskAsync<T>(name, args)` | `client.Functions.InvokeAsync<T>(name, args)` |
| `BmobUser.CurrentUser` | `client.CurrentUser` |
| `BmobInt` / `BmobBoolean` 包装类型 | 直接使用 `int` / `bool` |
| `BmobDebug.Register(...)` | 使用 `ILogger` 或自定义 `DelegatingHandler` |

旧 Demo（[bmob-demo-csharp](https://github.com/bmob/bmob-demo-csharp)）中的模型类需要
`readFields` / `write` 手工序列化；本 SDK 通过 `System.Text.Json` 自动完成。

## 构建与发布 NuGet

```bash
# 还原 + 编译 + 测试
dotnet build Bmob.Net.slnx -c Release
dotnet test tests/Bmob.Net.Tests/Bmob.Net.Tests.csproj -c Release

# 打包（生成 nupkg 与 snupkg）
dotnet pack src/Bmob.Net/Bmob.Net.csproj -c Release -o artifacts

# 发布到 nuget.org
dotnet nuget push artifacts/Bmob.Net.1.1.0.nupkg \
  --api-key <你的 NUGET_API_KEY> \
  --source https://api.nuget.org/v3/index.json
```

包内已包含：`README.md`、`LICENSE`、XML 文档、符号包（`.snupkg`），并声明了
`PackageId / Version / Authors / Description / Tags / RepositoryUrl / PackageLicenseExpression`。

发布前可修改 `src/Bmob.Net/Bmob.Net.csproj` 中的 `Version`、`Authors`、`PackageProjectUrl`、
`RepositoryUrl` 等字段。

## 包信息

| 项目 | 值 |
| --- | --- |
| Package ID | `Bmob.Net` |
| 当前版本 | `1.1.0` |
| 目标框架 | `net8.0`、`net10.0` |
| 许可证 | MIT |
| 依赖 | 无（仅 BCL） |
| 符号包 | `Bmob.Net.1.1.0.snupkg`（含 SourceLink） |
| 文档 | 包内附带 XML 注释文档 |

## 项目结构

```
src/Bmob.Net/                 SDK 主体（net8.0 / net10.0）
  BmobClient.cs               客户端与 HTTP 传输
  BmobObject.cs               数据对象
  BmobUser.cs                 用户对象
  BmobQuery.cs                查询构造器
  BmobBatch.cs                批量操作
  BmobDate/File/Pointer...    特殊数据类型
  Services/                   数据、用户、文件、云函数、BQL、批量、推送、短信
tests/Bmob.Net.Tests/         单元测试（xUnit）
samples/Bmob.Net.Sample/      控制台示例
```

## 常见问题

### 文件上传失败

- **HTTP 401/403 或提示绑定域名**：新版 Bmob 应用必须在控制台「设置 → 域名配置」绑定已备案域名，
  并把 `BmobClientOptions.BaseUrl`（示例中对应环境变量 `BMOB_BASE_URL`）设为该域名。
  默认的 `https://api.bmobcloud.com` 对未绑定域名的应用不可用。
- 上传接口为 `POST /2/files/{filename}`，`Content-Type` 必须与文件类型一致；
  SDK 按扩展名自动推断，可用 `UploadAsync(data, fileName, contentType)` 显式指定。
- 确认应用已开通文件存储服务，且 REST API Key 未被禁用。

### 云函数调用失败

- **函数不存在**：云函数必须先在 Bmob 云引擎中部署。示例默认调用 `first`
  （来自 bmob-demo-csharp 演示应用），你自己的应用需要部署同名函数，
  或通过环境变量 `BMOB_FUNCTION_NAME` 指定已部署的函数名。
- **返回结构与 `T` 不匹配**：先用 `Functions.InvokeAsync(functionName)` 拿原始 JSON 确认返回结构，
  再改用 `InvokeAsync<T>`。
- 云引擎代码抛异常时，Bmob 会返回业务错误码，可通过 `BmobException.Code` / `ResponseBody` 查看。

### 其他

- 业务码 `211`：Session Token 已失效，重新登录。
- 业务码 `122`：ACL 权限不足，检查对象 ACL 或使用 Master Key。
- 所有错误都可通过 `BmobException.StatusCode`（HTTP 状态码）、`Code`（业务码）、
  `ResponseBody`（原始响应）定位。

## 说明与限制

- REST API Key 会随请求头发送，仅可用于可信环境；浏览器/小程序场景应使用 Bmob 的加密授权或自建 BFF。
- Master Key 绕过所有 ACL，绝不可放入客户端。
- 查询默认返回 10 条，单次上限通常为 1000 条；更多数据请使用 `Skip` / `Limit` 分页或 `CountAsync`。
- Bmob 服务端返回的时间为 UTC，SDK 统一使用 `DateTimeOffset`（UTC）表示。
- 文件删除依赖上传返回的 `cdn` 字段；历史应用可能是 `upyun`，新应用通常为 `aliyun`。

## 许可证

MIT
