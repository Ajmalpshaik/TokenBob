using System.Net.Http;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using TokenBob.Core.Usage;

namespace TokenBob.App.Account;

/// <summary>Runs a same-origin fetch inside a WebView2 page and returns status + body.</summary>
internal static class WebViewFetch
{
    public static async Task<WebResponse> GetAsync(CoreWebView2 core, string path, CancellationToken ct)
    {
        var expression =
            $"fetch({JsonSerializer.Serialize(path)}, {{ credentials: 'include', headers: {{ accept: 'application/json' }} }})" +
            ".then(async r => JSON.stringify({ status: r.status, body: await r.text() }))";
        var parameters = JsonSerializer.Serialize(new { expression, awaitPromise = true, returnByValue = true });

        var raw = await core.CallDevToolsProtocolMethodAsync("Runtime.evaluate", parameters).WaitAsync(ct);

        using var reply = JsonDocument.Parse(raw);
        if (reply.RootElement.TryGetProperty("exceptionDetails", out _))
            throw new HttpRequestException("The request could not reach claude.ai.");

        var value = reply.RootElement.GetProperty("result").GetProperty("value").GetString() ?? "{}";
        using var inner = JsonDocument.Parse(value);
        return new WebResponse(
            inner.RootElement.GetProperty("status").GetInt32(),
            inner.RootElement.GetProperty("body").GetString() ?? "");
    }

    public static bool LooksLikeJson(string body)
    {
        var trimmed = body.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }
}
