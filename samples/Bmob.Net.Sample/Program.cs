using System.Text.Json;
using Bmob;

// 运行示例前请设置以下环境变量：
//   BMOB_APP_ID / BMOB_REST_KEY（必填）
//   BMOB_MASTER_KEY / BMOB_BASE_URL（可选）
var applicationId = "e23b8dd8985bf148642b176fb44cd7d9";
var restApiKey ="677f1bccf6d5cf718cbee89748a31710";

if (string.IsNullOrWhiteSpace(applicationId) || string.IsNullOrWhiteSpace(restApiKey))
{
    Console.WriteLine("请先设置环境变量 BMOB_APP_ID 和 BMOB_REST_KEY。");
    Console.WriteLine("示例：export BMOB_APP_ID=xxx && export BMOB_REST_KEY=yyy");
    return;
}

using var client = BmobClient.Create(
    applicationId,
    restApiKey,
    Environment.GetEnvironmentVariable("18137482f3812f4b6b69ec4696ebe2c2"),
    Environment.GetEnvironmentVariable("BMOB_BASE_URL"));

const string tableName = "Game";

Console.WriteLine("== 服务器时间 ==");
Console.WriteLine(await client.GetServerTimeAsync());

Console.WriteLine();
Console.WriteLine("== 新增数据 ==");
var game = new BmobObject(tableName)
    .Set("score", Random.Shared.Next(0, 100))
    .Set("playerName", "Bmob.Net")
    .Set("cheatMode", false);

await client.Objects.CreateAsync(game);
Console.WriteLine($"objectId = {game.ObjectId}");

Console.WriteLine();
Console.WriteLine("== 查询数据 ==");
var page = await client.Objects.Query(tableName)
    .WhereGreaterThanOrEqualTo("score", 0)
    .OrderByDescending("createdAt")
    .Limit(5)
    .FindAsync();

foreach (var item in page.Results)
{
    Console.WriteLine($"{item.ObjectId} | {item.GetString("playerName")} | {item.GetInt("score")}");
}

Console.WriteLine();
Console.WriteLine("== 更新数据（原子计数） ==");
await client.Objects.UpdateAsync(game.Increment("score", 10));
Console.WriteLine("score +10 完成");

Console.WriteLine();
Console.WriteLine("== 强类型映射 ==");
var typed = await client.Objects.GetAsAsync<GameScore>(tableName, game.ObjectId!);
Console.WriteLine(JsonSerializer.Serialize(typed));

Console.WriteLine();
Console.WriteLine("== 异步分页遍历 ==");
var all = await client.Objects.Query(tableName).Limit(2).FindAllAsync(maxRecords: 1000);
Console.WriteLine($"FindAllAsync 共读取 {all.Count} 条");

await foreach (var item in client.Objects.Query(tableName).AsAsyncEnumerable(pageSize: 2))
{
    Console.WriteLine($"流式读取：{item.ObjectId}");
    break;
}

Console.WriteLine();
Console.WriteLine("== SaveAsync（ObjectId 为空则新增，否则更新） ==");
game.Set("cheatMode", true);
await client.Objects.SaveAsync(game);
Console.WriteLine("保存完成");

Console.WriteLine();
Console.WriteLine("== 用户注册 / 登录 ==");
var username = "sdk_" + Guid.NewGuid().ToString("N")[..8];
var password = "Bmob@123456";

try
{
    var registered = await client.Users.RegisterAsync(username, password, $"{username}@example.com");
    Console.WriteLine($"注册成功：{registered.ObjectId}");

    await client.Users.LogoutAsync();

    var logged = await client.Users.LoginAsync(username, password);
    Console.WriteLine($"登录成功：{logged.Username}, sessionToken={logged.SessionToken}");

    var current = await client.Users.GetCurrentAsync();
    Console.WriteLine($"当前用户：{current?.Username}");
}
catch (BmobException exception)
{
    Console.WriteLine($"用户流程失败（code={exception.Code}）：{exception.Message}");
}

Console.WriteLine();
Console.WriteLine("== 文件上传 ==");
var tempFile = Path.Combine(Path.GetTempPath(), "bmob-sdk-sample.txt");
await File.WriteAllTextAsync(tempFile, "Hello Bmob from .NET SDK");

try
{
    var file = await client.Files.UploadAsync(tempFile);
    Console.WriteLine($"文件 URL：{file.Url}");
}
catch (BmobException exception)
{
    Console.WriteLine($"上传失败：HTTP {(int?)exception.StatusCode}，业务码 {exception.Code}，{exception.Error ?? exception.Message}");
    if (!string.IsNullOrEmpty(exception.ResponseBody))
    {
        Console.WriteLine($"响应体：{exception.ResponseBody}");
    }
}
catch (Exception exception)
{
    Console.WriteLine($"上传失败（非 Bmob 错误）：{exception.GetType().Name} - {exception.Message}");
}

Console.WriteLine();
Console.WriteLine("== 云函数调用 ==");
var functionName = Environment.GetEnvironmentVariable("BMOB_FUNCTION_NAME") ?? "first";

try
{
    var result = await client.Functions.InvokeAsync<Dictionary<string, string>>(functionName);
    Console.WriteLine($"云函数返回：{string.Join(", ", (result ?? new()).Select(pair => $"{pair.Key}={pair.Value}"))}");
}
catch (BmobException exception)
{
    Console.WriteLine($"调用失败：HTTP {(int?)exception.StatusCode}，业务码 {exception.Code}，{exception.Error ?? exception.Message}");
    Console.WriteLine($"提示：请确认云引擎中已部署名为 {functionName} 的函数（可用环境变量 BMOB_FUNCTION_NAME 指定）。");
    if (!string.IsNullOrEmpty(exception.ResponseBody))
    {
        Console.WriteLine($"响应体：{exception.ResponseBody}");
    }
}
catch (Exception exception)
{
    Console.WriteLine($"调用失败（非 Bmob 错误）：{exception.GetType().Name} - {exception.Message}");
}

Console.WriteLine();
Console.WriteLine("== 批量操作 ==");
var batch = new BmobBatch()
    .Create(new BmobObject(tableName).Set("playerName", "batch-a").Set("score", 1))
    .Create(new BmobObject(tableName).Set("playerName", "batch-b").Set("score", 2));

var batchResults = await client.Batch.ExecuteAsync(batch);
Console.WriteLine($"成功 {batchResults.Count(result => result.IsSuccess)} 条，失败 {batchResults.Count(result => !result.IsSuccess)} 条");

Console.WriteLine();
Console.WriteLine("== 删除数据 ==");
await client.Objects.DeleteAsync(game);
Console.WriteLine("删除完成");

Console.WriteLine();
Console.WriteLine("== BQL 查询 ==");
try
{
    var rows = await client.Bql.FindAsync<GameScore>(
        $"select * from {tableName} limit 0,3");
    Console.WriteLine($"BQL 返回 {rows.Count} 条");
}
catch (BmobException exception)
{
    Console.WriteLine($"BQL 查询失败（code={exception.Code}）：{exception.Message}");
}

/// <summary>
/// 与数据表字段对应的强类型模型（属性名按 camelCase 映射）。
/// </summary>
internal sealed class GameScore
{
    public string? ObjectId { get; set; }

    public string? PlayerName { get; set; }

    public int? Score { get; set; }

    public bool? CheatMode { get; set; }

    public DateTimeOffset? CreatedAt { get; set; }
}
