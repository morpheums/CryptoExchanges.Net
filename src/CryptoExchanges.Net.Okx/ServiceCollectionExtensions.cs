using CryptoExchanges.Net.Okx.Auth;
using CryptoExchanges.Net.Okx.Internal;
using CryptoExchanges.Net.Okx.Resilience;
using CryptoExchanges.Net.Core;
using CryptoExchanges.Net.Core.Enums;
using CryptoExchanges.Net.Core.Interfaces;
using CryptoExchanges.Net.Core.Resilience;
using CryptoExchanges.Net.Http;
using DeltaMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

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
        Action<OkxOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IExchangeClientFactory, ExchangeClientFactory>();

        services.AddOptions<OkxOptions>()
            .Configure(ApplyEnvDefaults)
            .Configure(o => configure?.Invoke(o))
            .Validate(o => o.TimeoutSeconds > 0, "OkxOptions.TimeoutSeconds must be > 0.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.BaseUrl), "OkxOptions.BaseUrl is required.")
            .ValidateOnStart();

        services.TryAddSingleton(sp => sp.GetRequiredService<IOptions<OkxOptions>>().Value);

        // Shared clock-skew offset holder (single-element array so the signing handler's closure and
        // SyncServerTimeAsync read/write the SAME instance). One per exchange registration.
        // CA1861: must be a FRESH mutable instance per registration (not a shared static field), and the
        // factory runs once for the singleton — so the "constant array" rule doesn't apply.
#pragma warning disable CA1861
        services.TryAddKeyedSingleton(ExchangeId.Okx, (_, _) => new long[] { 0L });
#pragma warning restore CA1861
        services.TryAddKeyedSingleton<ISymbolMapper>(ExchangeId.Okx,
            (_, _) => new SymbolMapper(OkxSymbolFormat.Instance));
        services.TryAddKeyedSingleton<IMapper>(ExchangeId.Okx, (sp, _) =>
            OkxClientComposer.CreateMapper(sp.GetRequiredKeyedService<ISymbolMapper>(ExchangeId.Okx)));

        // NAMED client (not typed): a typed client registers IOkxHttpClient as TRANSIENT, and capturing
        // it inside the keyed IExchangeClient SINGLETON below would be a captive dependency (the
        // HttpClient + handler chain would never rotate). Instead we resolve ONE long-lived HttpClient
        // via IHttpClientFactory.CreateClient(OkxClientName) in the singleton factory; DNS rotation is
        // handled by PooledConnectionLifetime on the primary handler.
        var http = services.AddHttpClient(OkxClientName, (sp, c) =>
            {
                var o = sp.GetRequiredService<OkxOptions>();
                // Host-only BaseAddress (no path) so RequestUri.PathAndQuery == the signed OKX requestPath.
                c.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/'));
                c.Timeout = TimeSpan.FromSeconds(o.TimeoutSeconds);
                c.DefaultRequestHeaders.Add("User-Agent", "CryptoExchanges.Net/0.1.0");
            })
            .ConfigurePrimaryHttpMessageHandler(() =>
                new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(2) });

        // Handler chain — applied via the Http project's shared seam so the DI path and the
        // container-free Create() path (HttpClientPipelineBuilder.Build) compose the SAME effective
        // pipeline. Order (outer→inner): throttle → exhaustion-mapping → Polly(retry/timeout) →
        // request-finalizer(signing) → error-translation → primary transport.
        //
        // The finalizer is ALWAYS registered: options are only final at resolution time, so the gate is
        // decided then. OKX signing requires BOTH a secret AND a passphrase; if either is missing the
        // client gets a no-op PassThroughHandler (mirrors Create()). Because the gate requires both,
        // the secret/passphrase are passed to OkxSigningHandler directly — no OkxOptions.ToCredentials()
        // (which throws on an empty passphrase) is needed in the signing path.
        http.ApplyResiliencePipeline(
            OkxClientName,
            new ResilienceOptions(),
            translatorFactory: _ => new OkxErrorTranslator(),
            gateFactory: _ => new ReactiveRateLimitGate(),
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
            });

        services.AddKeyedSingleton<IExchangeClient>(ExchangeId.Okx, (sp, _) =>
        {
            var httpClient = sp.GetRequiredService<IHttpClientFactory>().CreateClient(OkxClientName);
            var http = new OkxHttpClient(httpClient);
            var holder = sp.GetRequiredKeyedService<long[]>(ExchangeId.Okx);
            return OkxClientComposer.ComposeForDi(sp, http, holder);
        });

        return services;
    }

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
