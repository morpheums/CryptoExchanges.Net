namespace CryptoExchanges.Net.Http.Streaming;

/// <summary>
/// Maps each <see cref="StreamKind"/> to its decode closure for the engine.
/// Closures are opaque <c>Func&lt;ReadOnlyMemory&lt;byte&gt;, object&gt;</c> built in the
/// exchange package (binding constraint K1: DTO decode + model mapping stays exchange-side);
/// the Http engine sees only raw bytes in and <see langword="object"/> out.
/// </summary>
internal sealed class StreamDecoderRegistry
{
    private readonly Dictionary<StreamKind, Func<ReadOnlyMemory<byte>, object>> _decoders = [];

    /// <summary>
    /// Registers a decode closure for the given <paramref name="kind"/>.
    /// Overwrites any previously registered closure for that kind.
    /// </summary>
    /// <param name="kind">The stream kind this closure handles.</param>
    /// <param name="decoder">
    /// The opaque decode closure: accepts raw frame bytes, returns the decoded model
    /// as <see langword="object"/>. The engine casts to <c>T</c> before delivering
    /// to the subscription channel.
    /// </param>
    public void Register(StreamKind kind, Func<ReadOnlyMemory<byte>, object> decoder)
    {
        ArgumentNullException.ThrowIfNull(decoder);
        _decoders[kind] = decoder;
    }

    /// <summary>
    /// Resolves the decode closure for the given <paramref name="kind"/>.
    /// </summary>
    /// <param name="kind">The stream kind to look up.</param>
    /// <returns>The registered decode closure.</returns>
    /// <exception cref="InvalidOperationException">
    /// No closure has been registered for <paramref name="kind"/>.
    /// </exception>
    public Func<ReadOnlyMemory<byte>, object> Resolve(StreamKind kind)
    {
        if (!_decoders.TryGetValue(kind, out var decoder))
            throw new InvalidOperationException(
                $"No decode closure registered for {nameof(StreamKind)}.{kind}. " +
                $"Ensure the exchange package registers all four stream kinds before use.");
        return decoder;
    }

    /// <summary>
    /// Returns <see langword="true"/> if a closure is registered for <paramref name="kind"/>.
    /// </summary>
    public bool Contains(StreamKind kind) => _decoders.ContainsKey(kind);
}
