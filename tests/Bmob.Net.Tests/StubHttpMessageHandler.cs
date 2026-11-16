using System.Net;
using System.Text;

namespace Bmob.Net.Tests;

/// <summary>
/// 记录请求并按队列返回预设响应的测试用 HttpMessageHandler。
/// </summary>
internal sealed class StubHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<(HttpStatusCode Status, string Body)> _responses = new();

    public List<RequestSnapshot> Requests { get; } = new();

    public StubHttpMessageHandler Respond(HttpStatusCode status, string body)
    {
        _responses.Enqueue((status, body));
        return this;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var body = request.Content is null
            ? null
            : await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in request.Headers)
        {
            headers[header.Key] = string.Join(",", header.Value);
        }

        Requests.Add(new RequestSnapshot
        {
            Method = request.Method,
            Uri = request.RequestUri!,
            Body = body,
            ContentType = request.Content?.Headers.ContentType?.MediaType,
            Headers = headers,
        });

        if (_responses.Count == 0)
        {
            throw new InvalidOperationException($"未为请求 {request.Method} {request.RequestUri} 配置响应。");
        }

        var (status, responseBody) = _responses.Dequeue();

        return new HttpResponseMessage(status)
        {
            Content = new StringContent(responseBody, Encoding.UTF8, "application/json"),
        };
    }
}

internal sealed class RequestSnapshot
{
    public required HttpMethod Method { get; init; }

    public required Uri Uri { get; init; }

    public string? Body { get; init; }

    public string? ContentType { get; init; }

    public required IReadOnlyDictionary<string, string> Headers { get; init; }

    public string? Header(string name) => Headers.TryGetValue(name, out var value) ? value : null;
}
