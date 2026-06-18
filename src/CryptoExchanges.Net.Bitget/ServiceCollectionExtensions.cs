using CryptoExchanges.Net.Bitget.Auth;
using CryptoExchanges.Net.Bitget.Internal;
using CryptoExchanges.Net.Bitget.Resilience;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Resilience;
using CryptoExchanges.Net.Http;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Bitget;

/// <summary>
/// Dependency-injection extensions for registering the Bitget exchange client. Lives in the Bitget
/// assembly so a consumer can depend on Bitget alone without transitively pulling in other exchange
/// assemblies (see ADR-001).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Named-client / resilience-pipeline name for the Bitget HTTP client — shared by the
    /// registration and the <c>CreateClient</c> call so they can't drift.</summary>
    private const string BitgetClientName = "bitget";

    /// <summary>
    /// Registers the Bitget exchange client and all its dependencies as per-exchange keyed singletons,
    /// backed by a typed <see cref="System.Net.Http.HttpClient"/> with the full resilience handler chain.
    /// Options are validated with fail-fast (<c>ValidateOnStart</c>).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An action to configure <see cref="BitgetOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddBitgetExchange(
        this IServiceCollection services,
        Action<BitgetOptions>? configure = null) =>
        // Delegates to the shared Http registration helper; only the Bitget variation points differ.
        // Bitget sets NO default api-key header (it signs per-request), so configureHttpClient is null,
        // and BitgetHttpClient's ctor takes (httpClient) only. Bitget uses the default ResilienceOptions
        // (no usage-header — Bitget V2 exposes no documented usage-fraction header). The finalizer is
        // ALWAYS registered and gates on BOTH SecretKey AND Passphrase; if either is missing the client
        // resolves to a no-op PassThroughHandler (mirrors Create()). Because the gate requires both,
        // secret/passphrase are passed to BitgetSigningHandler directly — no BitgetOptions.ToCredentials()
        // (which throws on an empty passphrase) in the signing path.
        ExchangeServiceRegistration.AddExchange<BitgetOptions, IMapper>(
            services,
            ExchangeId.Bitget,
            BitgetClientName,
            optionsName: "BitgetOptions",
            applyEnvDefaults: ApplyEnvDefaults,
            configure: configure,
            timeoutSecondsSelector: o => o.TimeoutSeconds,
            // Host-only BaseAddress (no path) so RequestUri.AbsolutePath/Query == the signed Bitget
            // requestPath/query. NormalizeHostRoot fails fast if a path segment is present (TASK-021).
            baseUrlSelector: o => BitgetClientComposer.NormalizeHostRoot(o.BaseUrl),
            symbolMapperFactory: () => new SymbolMapper(BitgetSymbolFormat.Instance),
            mapperFactory: BitgetClientComposer.CreateMapper,
            configureHttpClient: null,
            resilienceOptions: new ResilienceOptions(),
            translatorFactory: _ => new BitgetErrorTranslator(),
            requestFinalizerFactory: sp =>
            {
                var o = sp.GetRequiredService<BitgetOptions>();
                if (string.IsNullOrEmpty(o.SecretKey) || string.IsNullOrEmpty(o.Passphrase))
                    return new PassThroughHandler();
                var holder = sp.GetRequiredKeyedService<long[]>(ExchangeId.Bitget);
                return new BitgetSigningHandler(
                    o.ApiKey,
                    o.Passphrase,
                    new BitgetSignatureService(o.SecretKey),
                    () => Interlocked.Read(ref holder[0]));
            },
            exchangeClientFactory: (sp, httpClient, holder) =>
                BitgetClientComposer.ComposeForDi(sp, new BitgetHttpClient(httpClient), holder));

    private static void ApplyEnvDefaults(BitgetOptions o)
    {
        var k = Environment.GetEnvironmentVariable("BITGET_API_KEY");
        var s = Environment.GetEnvironmentVariable("BITGET_SECRET_KEY");
        var p = Environment.GetEnvironmentVariable("BITGET_PASSPHRASE");
        if (!string.IsNullOrEmpty(k)) o.ApiKey = k;
        if (!string.IsNullOrEmpty(s)) o.SecretKey = s;
        if (!string.IsNullOrEmpty(p)) o.Passphrase = p;
    }
}
