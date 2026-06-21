namespace CryptoExchanges.Net.Kucoin.Dtos.Streaming;

/// <summary>
/// Payload returned by <c>POST /api/v1/bullet-public</c> — the public WebSocket
/// negotiation endpoint. Wrapped in a <see cref="ResponseDto{T}"/> envelope.
/// </summary>
internal sealed record BulletPublicDto
{
    /// <summary>Short-lived connection token to embed in the WebSocket URI query string.</summary>
    [JsonPropertyName("token")]
    public string Token { get; init; } = string.Empty;

    /// <summary>List of candidate WebSocket instance servers. The client picks the first entry.</summary>
    [JsonPropertyName("instanceServers")]
    public List<InstanceServerDto> InstanceServers { get; init; } = [];
}

/// <summary>
/// A single WebSocket server entry inside <see cref="BulletPublicDto.InstanceServers"/>.
/// </summary>
internal sealed record InstanceServerDto
{
    /// <summary>WebSocket endpoint URL (e.g. <c>wss://ws-api-spot.kucoin.com/</c>).</summary>
    [JsonPropertyName("endpoint")]
    public string Endpoint { get; init; } = string.Empty;

    /// <summary>Server-dictated client-ping interval in milliseconds.</summary>
    [JsonPropertyName("pingInterval")]
    public int PingInterval { get; init; }

    /// <summary>Server-dictated ping timeout in milliseconds.</summary>
    [JsonPropertyName("pingTimeout")]
    public int PingTimeout { get; init; }
}
