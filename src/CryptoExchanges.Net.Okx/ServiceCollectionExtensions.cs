using CryptoExchanges.Net.Okx.Auth;
using CryptoExchanges.Net.Okx.Internal;
using CryptoExchanges.Net.Okx.Resilience;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Resilience;
using CryptoExchanges.Net.Http;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;

namespace CryptoExchanges.Net.Okx;

/// <summary>
/// Dependency-injection extensions for registering the OKX exchange client. Lives in the OKX assembly
/// so a consumer can depend on OKX alone without transitively pulling in other exchange assemblies
/// (see ADR-001).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>Named-client / resilience-pipeline name for the OKX HTTP client — shared by the
    /// registration and the <c>CreateClient</c> call so they can't drift.</summary>
    private const string OkxClientName = "okx";

    /// <summary>
    /// Registers the OKX exchange client and all its dependencies as per-exchange keyed singletons,
    /// backed by a typed <see cref="System.Net.Http.HttpClient"/> with the full resilience handler chain.
    /// Options are validated with fail-fast (<c>ValidateOnStart</c>).
    /// </summary>
    /// <param name="services">The <see cref="IServiceCollection"/> to add services to.</param>
    /// <param name="configure">An action to configure <see cref="OkxOptions"/>.</param>
    /// <returns>The <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddOkxExchange(
        this IServiceCollection services,
        Action<OkxOptions>? configure = null) =>
        // Delegates to the shared Http registration helper; only the OKX variation points differ.
        // OKX sets NO default api-key header (it signs per-request), so configureHttpClient is null,
        // and OkxHttpClient's ctor takes (httpClient) only. OKX uses the default ResilienceOptions
        // (no usage-header). The finalizer is ALWAYS registered and gates on BOTH SecretKey AND
        // Passphrase; if either is missing the client resolves to a no-op PassThroughHandler (mirrors
        // Create()). Because the gate requires both, secret/passphrase are passed to OkxSigningHandler
        // directly — no OkxOptions.ToCredentials() (which throws on an empty passphrase) in the signing path.
        ExchangeServiceRegistration.AddExchange<OkxOptions, IMapper>(
            services,
            ExchangeId.Okx,
            OkxClientName,
            optionsName: "OkxOptions",
            applyEnvDefaults: ApplyEnvDefaults,
            configure: configure,
            timeoutSecondsSelector: o => o.TimeoutSeconds,
            // Host-only BaseAddress (no path) so RequestUri.PathAndQuery == the signed OKX requestPath.
            // ExchangeUrl.NormalizeHostRoot fails fast if a path segment is present (would break the prehash).
            baseUrlSelector: o => ExchangeUrl.NormalizeHostRoot(o.BaseUrl),
            symbolMapperFactory: () => new SymbolMapper(OkxSymbolFormat.Instance),
            mapperFactory: OkxClientComposer.CreateMapper,
            configureHttpClient: null,
            resilienceOptions: new ResilienceOptions(),
            translatorFactory: _ => new OkxErrorTranslator(),
            requestFinalizerFactory: sp =>
            {
                var o = sp.GetRequiredService<OkxOptions>();
                if (string.IsNullOrEmpty(o.SecretKey) || string.IsNullOrEmpty(o.Passphrase))
                    return new PassThroughHandler();
                var holder = sp.GetRequiredKeyedService<long[]>(ExchangeId.Okx);
                return new OkxSigningHandler(
                    o.ApiKey,
                    o.Passphrase,
                    new OkxSignatureService(o.SecretKey),
                    () => Interlocked.Read(ref holder[0]));
            },
            exchangeClientFactory: (sp, httpClient, holder) =>
                OkxClientComposer.ComposeForDi(sp, new OkxHttpClient(httpClient), holder));

    private static void ApplyEnvDefaults(OkxOptions o)
    {
        var k = Environment.GetEnvironmentVariable("OKX_API_KEY");
        var s = Environment.GetEnvironmentVariable("OKX_SECRET_KEY");
        var p = Environment.GetEnvironmentVariable("OKX_PASSPHRASE");
        if (!string.IsNullOrEmpty(k)) o.ApiKey = k;
        if (!string.IsNullOrEmpty(s)) o.SecretKey = s;
        if (!string.IsNullOrEmpty(p)) o.Passphrase = p;
    }
}
