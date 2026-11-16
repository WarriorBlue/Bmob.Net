using System.Net;
using System.Text.Json;
using Bmob.Services;
using Xunit;

namespace Bmob.Net.Tests;

public class BmobClientTests
{
    [Fact]
    public async Task CreateAsync_PostsToClasses_AndFillsObjectId()
    {
        var (client, handler) = TestClient.Create();
        handler.Respond(HttpStatusCode.Created, """{"createdAt":"2024-01-01 10:00:00","objectId":"abc123"}""");

        var data = new BmobObject("Game")
            .Set("score", 1337)
            .Set("playerName", "Sean Plott")
            .Set("cheatMode", false);

        var created = await client.Objects.CreateAsync(data);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.example.com/1/classes/Game", request.Uri.ToString());
        Assert.Equal("app-id", request.Header("X-Bmob-Application-Id"));
        Assert.Equal("rest-key", request.Header("X-Bmob-REST-API-Key"));
        Assert.Equal("application/json", request.ContentType);

        using var body = JsonDocument.Parse(request.Body!);
        Assert.Equal(1337, body.RootElement.GetProperty("score").GetInt32());
        Assert.Equal("Sean Plott", body.RootElement.GetProperty("playerName").GetString());
        Assert.False(body.RootElement.GetProperty("cheatMode").GetBoolean());
        Assert.False(body.RootElement.TryGetProperty("objectId", out _));

        Assert.Equal("abc123", created.ObjectId);
        Assert.Equal(new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero), created.CreatedAt);
    }

    [Fact]
    public async Task Query_BuildsParameters_AndParsesResults()
    {
        var (client, handler) = TestClient.Create();
        handler.Respond(HttpStatusCode.OK, """{"results":[{"objectId":"a1","score":10}],"count":1}""");

        var result = await client.Objects.Query("Game")
            .WhereGreaterThan("score", 5)
            .OrderByDescending("createdAt")
            .Include("author")
            .Select("score")
            .Limit(20)
            .FindAsync();

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, request.Method);

        var where = TestClient.QueryValue(request.Uri, "where");
        using var whereJson = JsonDocument.Parse(where!);
        Assert.Equal(5, whereJson.RootElement.GetProperty("score").GetProperty("$gt").GetInt32());
        Assert.Equal("-createdAt", TestClient.QueryValue(request.Uri, "order"));
        Assert.Equal("author", TestClient.QueryValue(request.Uri, "include"));
        Assert.Equal("score", TestClient.QueryValue(request.Uri, "keys"));
        Assert.Equal("20", TestClient.QueryValue(request.Uri, "limit"));

        var item = Assert.Single(result.Results);
        Assert.Equal("a1", item.ObjectId);
        Assert.Equal(10, item.GetInt("score"));
        Assert.Equal(1, result.Count);
    }

    [Fact]
    public async Task UpdateAsync_SendsOnlyDirtyFields()
    {
        var (client, handler) = TestClient.Create();
        handler.Respond(HttpStatusCode.OK, """{"updatedAt":"2024-01-02 08:30:00"}""");

        var data = new BmobObject("Game").Set("score", 100).Set("playerName", "old");
        data.ObjectId = "abc123";

        // 模拟从服务端加载后清空脏字段
        _ = data.UpdatePayload();
        data.ClearDirtyFields();

        data.Set("playerName", "bmob后端云");

        await client.Objects.UpdateAsync(data);

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Put, request.Method);
        Assert.Equal("https://api.example.com/1/classes/Game/abc123", request.Uri.ToString());
        Assert.Equal("""{"playerName":"bmob后端云"}""", request.Body);
        Assert.Equal(new DateTimeOffset(2024, 1, 2, 8, 30, 0, TimeSpan.Zero), data.UpdatedAt);
    }

    [Fact]
    public async Task DeleteAsync_SendsDeleteRequest()
    {
        var (client, handler) = TestClient.Create();
        handler.Respond(HttpStatusCode.OK, """{"msg":"ok"}""");

        await client.Objects.DeleteAsync("Game", "abc123");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Delete, request.Method);
        Assert.Equal("https://api.example.com/1/classes/Game/abc123", request.Uri.ToString());
    }

    [Fact]
    public async Task ErrorResponse_Throws_BmobExceptionWithCode()
    {
        var (client, handler) = TestClient.Create();
        handler.Respond(HttpStatusCode.BadRequest, """{"code":105,"error":"invalid field name: bl!ng"}""");

        var exception = await Assert.ThrowsAsync<BmobException>(
            () => client.Objects.CreateAsync(new BmobObject("Game").Set("bl!ng", 1)));

        Assert.Equal(105, exception.Code);
        Assert.Equal(HttpStatusCode.BadRequest, exception.StatusCode);
        Assert.Equal("invalid field name: bl!ng", exception.Error);
        Assert.Contains("invalid field name", exception.Message);
    }

    [Fact]
    public async Task LoginAsync_SetsCurrentUser_AndUsesSessionToken()
    {
        var (client, handler) = TestClient.Create();
        handler
            .Respond(HttpStatusCode.OK, """{"objectId":"u1","username":"tom","sessionToken":"sess-1"}""")
            .Respond(HttpStatusCode.OK, """{"objectId":"u1","username":"tom","email":"tom@example.com"}""");

        var user = await client.Users.LoginAsync("tom", "123456");

        Assert.Equal("u1", user.ObjectId);
        Assert.Equal("sess-1", user.SessionToken);
        Assert.Same(user, client.CurrentUser);

        var current = await client.Users.GetCurrentAsync();

        Assert.NotNull(current);
        Assert.Equal("tom@example.com", current.Email);
        Assert.Equal("sess-1", current.SessionToken);

        var loginRequest = handler.Requests[0];
        Assert.Equal("https://api.example.com/1/login", loginRequest.Uri.ToString());
        Assert.Equal("""{"username":"tom","password":"123456"}""", loginRequest.Body);

        var currentRequest = handler.Requests[1];
        Assert.Equal("sess-1", currentRequest.Header("X-Bmob-Session-Token"));
    }

    [Fact]
    public async Task FileUpload_SendsBinaryBody_AndParsesResult()
    {
        var (client, handler) = TestClient.Create();
        handler.Respond(HttpStatusCode.OK, """{"filename":"hello.png","url":"https://cdn.example.com/2024/hello.png","cdn":"aliyun"}""");

        var file = await client.Files.UploadAsync(new byte[] { 1, 2, 3 }, "hello.png", "image/png");

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("https://api.example.com/2/files/hello.png", request.Uri.ToString());
        Assert.Equal("image/png", request.ContentType);
        Assert.Equal("aliyun", file.Cdn);
        Assert.Equal("https://cdn.example.com/2024/hello.png", file.Url);
    }

    [Fact]
    public async Task Serialization_WritesBmobSpecialTypes()
    {
        var (client, handler) = TestClient.Create();
        handler.Respond(HttpStatusCode.Created, """{"objectId":"new1"}""");

        var data = new BmobObject("Post")
            .Set("title", "hello")
            .Set("author", new BmobPointer("_User", "u1"))
            .Set("location", new BmobGeoPoint(23.05, 113.4))
            .Set("publishedAt", new BmobDate(new DateTimeOffset(2024, 5, 1, 0, 0, 0, TimeSpan.Zero)))
            .Set("likes", BmobOperation.Increment(1))
            .Set("tags", new[] { "bmob", "sdk" });

        data.Acl = new BmobAcl().AllowPublicRead().AllowUser("u1", read: true, write: true);

        await client.Objects.CreateAsync(data);

        using var body = JsonDocument.Parse(handler.Requests[0].Body!);
        var root = body.RootElement;

        Assert.Equal("Pointer", root.GetProperty("author").GetProperty("__type").GetString());
        Assert.Equal("_User", root.GetProperty("author").GetProperty("className").GetString());
        Assert.Equal("u1", root.GetProperty("author").GetProperty("objectId").GetString());

        Assert.Equal("Date", root.GetProperty("publishedAt").GetProperty("__type").GetString());
        Assert.Equal("2024-05-01 00:00:00", root.GetProperty("publishedAt").GetProperty("iso").GetString());

        Assert.Equal("GeoPoint", root.GetProperty("location").GetProperty("__type").GetString());
        Assert.Equal(23.05, root.GetProperty("location").GetProperty("latitude").GetDouble());

        Assert.Equal("Increment", root.GetProperty("likes").GetProperty("__op").GetString());
        Assert.Equal(1, root.GetProperty("likes").GetProperty("amount").GetInt32());

        Assert.Equal(2, root.GetProperty("tags").GetArrayLength());
        Assert.True(root.GetProperty("ACL").GetProperty("*").GetProperty("read").GetBoolean());
        Assert.True(root.GetProperty("ACL").GetProperty("u1").GetProperty("write").GetBoolean());
    }

    [Fact]
    public async Task Batch_SendsRequests_AndParsesPerItemResults()
    {
        var (client, handler) = TestClient.Create();
        handler.Respond(
            HttpStatusCode.OK,
            """[{"success":{"objectId":"x1"}},{"error":{"code":105,"error":"bad field"}}]""");

        var batch = new BmobBatch()
            .Create(new BmobObject("Game").Set("score", 1))
            .Delete("Game", "old-id");

        var results = await client.Batch.ExecuteAsync(batch);

        using var body = JsonDocument.Parse(handler.Requests[0].Body!);
        var requests = body.RootElement.GetProperty("requests");
        Assert.Equal(2, requests.GetArrayLength());
        Assert.Equal("POST", requests[0].GetProperty("method").GetString());
        Assert.Equal("/1/classes/Game", requests[0].GetProperty("path").GetString());
        Assert.Equal("DELETE", requests[1].GetProperty("method").GetString());

        Assert.True(results[0].IsSuccess);
        Assert.Equal("x1", results[0].Data!.Value.GetProperty("objectId").GetString());
        Assert.False(results[1].IsSuccess);
        Assert.Equal(105, results[1].ErrorCode);
    }

    [Fact]
    public async Task CloudFunction_Invoke_DeserializesResult()
    {
        var (client, handler) = TestClient.Create();
        handler.Respond(HttpStatusCode.OK, """{"bmob":"Hello,world"}""");

        var result = await client.Functions.InvokeAsync<Dictionary<string, string>>("first", new { userid = "u1" });

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.example.com/1/functions/first", request.Uri.ToString());

        Assert.NotNull(result);
        Assert.Equal("Hello,world", result!["bmob"]);
    }

    [Fact]
    public async Task Bql_SendsQuery_AndMapsResults()
    {
        var (client, handler) = TestClient.Create();
        handler.Respond(HttpStatusCode.OK, """{"results":[{"score":42}]}""");

        var rows = await client.Bql.FindAsync<Dictionary<string, int>>(
            "select * from Game where score > ? limit 0,10",
            10);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("select * from Game where score > ? limit 0,10", TestClient.QueryValue(request.Uri, "bql"));
        Assert.Equal("[10]", TestClient.QueryValue(request.Uri, "values"));

        var row = Assert.Single(rows);
        Assert.Equal(42, row["score"]);
    }

    [Fact]
    public async Task CountAsync_SendsCountOnlyQuery()
    {
        var (client, handler) = TestClient.Create();
        handler.Respond(HttpStatusCode.OK, """{"results":[],"count":42}""");

        var count = await client.Objects.Query("Game").WhereEqualTo("cheatMode", false).CountAsync();

        Assert.Equal(42, count);
        Assert.Equal("0", TestClient.QueryValue(handler.Requests[0].Uri, "limit"));
        Assert.Equal("1", TestClient.QueryValue(handler.Requests[0].Uri, "count"));
    }

    [Fact]
    public async Task ServerTime_IsParsed()
    {
        var (client, handler) = TestClient.Create();
        handler.Respond(HttpStatusCode.OK, """{"timestamp":1437531770,"datetime":"2015-07-22 10:22:50"}""");

        var time = await client.GetServerTimeAsync();

        Assert.Equal(DateTimeOffset.FromUnixTimeSeconds(1437531770), time);
    }

    [Fact]
    public async Task GetAsync_MapsDateAndPointerFields()
    {
        var (client, handler) = TestClient.Create();
        handler.Respond(
            HttpStatusCode.OK,
            """
            {
              "objectId": "p1",
              "createdAt": "2024-03-01 12:00:00",
              "author": {"__type":"Pointer","className":"_User","objectId":"u1"},
              "publishedAt": {"__type":"Date","iso":"2024-02-01 08:00:00"}
            }
            """);

        var post = await client.Objects.GetAsync("Post", "p1");

        var author = post.Get<BmobPointer>("author");
        Assert.NotNull(author);
        Assert.Equal("u1", author!.ObjectId);

        var publishedAt = post.GetDateTime("publishedAt");
        Assert.Equal(new DateTimeOffset(2024, 2, 1, 8, 0, 0, TimeSpan.Zero), publishedAt);
    }

    [Fact]
    public async Task SaveAsync_Creates_WhenObjectIdMissing_Updates_WhenPresent()
    {
        var (client, handler) = TestClient.Create();
        handler
            .Respond(HttpStatusCode.Created, """{"objectId":"new1"}""")
            .Respond(HttpStatusCode.OK, """{"updatedAt":"2024-01-02 00:00:00"}""");

        var data = new BmobObject("Game").Set("score", 1);
        await client.Objects.SaveAsync(data);

        Assert.Equal("new1", data.ObjectId);
        Assert.Equal(HttpMethod.Post, handler.Requests[0].Method);

        data.Set("score", 2);
        await client.Objects.SaveAsync(data);

        Assert.Equal(HttpMethod.Put, handler.Requests[1].Method);
        Assert.Equal("""{"score":2}""", handler.Requests[1].Body);
    }

    [Fact]
    public async Task FindAllAsync_AutoPaginates_UntilLastPage()
    {
        var (client, handler) = TestClient.Create();
        handler
            .Respond(HttpStatusCode.OK, """{"results":[{"objectId":"a"},{"objectId":"b"},{"objectId":"c"}]}""")
            .Respond(HttpStatusCode.OK, """{"results":[{"objectId":"d"}]}""");

        var all = await client.Objects.Query("Game").Limit(3).FindAllAsync();

        Assert.Equal(new[] { "a", "b", "c", "d" }, all.Select(item => item.ObjectId).ToArray());
        Assert.Equal("3", TestClient.QueryValue(handler.Requests[0].Uri, "limit"));
        Assert.Equal("0", TestClient.QueryValue(handler.Requests[0].Uri, "skip"));
        Assert.Equal("3", TestClient.QueryValue(handler.Requests[1].Uri, "skip"));
    }

    [Fact]
    public async Task AsAsyncEnumerable_StreamsAllPages()
    {
        var (client, handler) = TestClient.Create();
        handler
            .Respond(HttpStatusCode.OK, """{"results":[{"objectId":"a"},{"objectId":"b"}]}""")
            .Respond(HttpStatusCode.OK, """{"results":[{"objectId":"c"}]}""");

        var ids = new List<string>();
        await foreach (var item in client.Objects.Query("Game").AsAsyncEnumerable(pageSize: 2))
        {
            ids.Add(item.ObjectId!);
        }

        Assert.Equal(new[] { "a", "b", "c" }, ids);
        Assert.Equal("2", TestClient.QueryValue(handler.Requests[1].Uri, "skip"));
    }

    [Fact]
    public async Task StreamUpload_KeepsCallerStreamOpen()
    {
        var (client, handler) = TestClient.Create();
        handler.Respond(HttpStatusCode.OK, """{"filename":"a.bin","url":"https://cdn.example.com/a.bin","cdn":"aliyun"}""");

        var stream = new MemoryStream(new byte[] { 9, 8, 7 }, writable: false);
        var file = await client.Files.UploadAsync(stream, "a.bin", "application/octet-stream");

        Assert.True(stream.CanRead);
        Assert.Equal("aliyun", file.Cdn);
    }
}
