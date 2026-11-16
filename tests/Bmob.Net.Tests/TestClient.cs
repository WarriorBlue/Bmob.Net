namespace Bmob.Net.Tests;

internal static class TestClient
{
    public static (BmobClient Client, StubHttpMessageHandler Handler) Create(Action<BmobClientOptions>? configure = null)
    {
        var handler = new StubHttpMessageHandler();
        var httpClient = new HttpClient(handler);

        var options = new BmobClientOptions
        {
            ApplicationId = "app-id",
            RestApiKey = "rest-key",
            MasterKey = "master-key",
            BaseUrl = "https://api.example.com",
        };

        configure?.Invoke(options);

        return (new BmobClient(options, httpClient), handler);
    }

    public static string? QueryValue(Uri uri, string name)
    {
        var query = uri.Query.TrimStart('?');

        foreach (var pair in query.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = pair.IndexOf('=');
            var key = separator < 0 ? pair : pair[..separator];

            if (string.Equals(Uri.UnescapeDataString(key), name, StringComparison.Ordinal))
            {
                return separator < 0 ? string.Empty : Uri.UnescapeDataString(pair[(separator + 1)..]);
            }
        }

        return null;
    }
}
