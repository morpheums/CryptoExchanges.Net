using System.Text.Json;
using System.Text.Json.Serialization;

namespace CryptoExchanges.Net.Core.Models;

/// <summary>Serializes <see cref="Asset"/> as its ticker string.</summary>
[System.Diagnostics.CodeAnalysis.SuppressMessage(
    "Performance",
    "CA1812:Avoid uninstantiated internal classes",
    Justification = "Instantiated by System.Text.Json via the [JsonConverter(typeof(...))] attribute on Asset.")]
internal sealed class AssetJsonConverter : JsonConverter<Asset>
{
    /// <inheritdoc/>
    public override Asset Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return Asset.None;

        if (reader.TokenType != JsonTokenType.String)
            throw new JsonException($"Expected a string token for {nameof(Asset)} but found {reader.TokenType}.");

        return Asset.TryOf(reader.GetString(), out var asset) ? asset : Asset.None;
    }

    /// <inheritdoc/>
    public override void Write(
        Utf8JsonWriter writer,
        Asset value,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        if (value.IsNone)
            writer.WriteNullValue();
        else
            writer.WriteStringValue(value.Ticker);
    }
}
