using System.Net;

namespace CryptoExchanges.Net.Http.Tests.Unit;

/// <summary>Test handler returning queued responses (or invoking a per-request factory).</summary>
public sealed class StubHandler(Func<HttpRequestMessage, int, HttpResponseMessage> responder)
    : HttpMessageHandler
{
    public int Calls { get; private set; }
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        var index = Calls;
        Calls++;
        var resp = responder(request, index);
        resp.RequestMessage = request;
        return Task.FromResult(resp);
    }
}
