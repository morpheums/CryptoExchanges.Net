using CryptoExchanges.Net.Core.Exceptions;

namespace CryptoExchanges.Net.Core.Interfaces;

/// <summary>
/// Translates a failed exchange HTTP response into a typed <see cref="ExchangeException"/>.
/// One implementation per exchange (maps that venue's error codes/status to the shared types).
/// Implementations handle per-response business errors; transient-exhaustion mapping is done
/// by the resilience pipeline, not here.
/// </summary>
public interface IExchangeErrorTranslator
{
    /// <summary>Maps a non-success response (and its already-read body) to a typed exception.</summary>
    ExchangeException Translate(HttpResponseMessage response, string body);
}
