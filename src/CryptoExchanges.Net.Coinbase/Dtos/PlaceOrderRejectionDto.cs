namespace CryptoExchanges.Net.Coinbase.Dtos;

/// <summary>The nested rejection object inside a failed place-order response.</summary>
internal sealed record PlaceOrderRejectionDto
{
    [JsonPropertyName("error")]
    public string Error { get; init; } = string.Empty;

    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    [JsonPropertyName("error_details")]
    public string ErrorDetails { get; init; } = string.Empty;
}
