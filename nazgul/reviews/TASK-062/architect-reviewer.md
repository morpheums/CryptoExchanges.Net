---
reviewer: architect-reviewer
verdict: APPROVED
---
# TASK-062 Architect Review

## Verdict: APPROVED

---

## Findings

### PASS — K1: Http layer untouched (confidence: 100%)

The diff adds files exclusively under `src/CryptoExchanges.Net.Kucoin/`. No file under `src/CryptoExchanges.Net.Http/` was modified. The Http layer contains zero `Core.Models` or DeltaMapper references in its new streaming infrastructure (`StreamConnectionInfo`, `HeartbeatPolicy`, `IStreamProtocol`, `StreamServiceRegistration` — all confirmed clean). K1 is intact.

---

### PASS — C1: No timers, threads, or Task.Delay in KucoinStreamProtocol or KucoinBulletPublicClient (confidence: 100%)

Grep of `src/CryptoExchanges.Net.Kucoin/Streaming/` for `Timer`, `Thread`, `Task.Delay`, and `Thread.Sleep` returned no results. `KucoinStreamProtocol` is pure classification + data construction; `HeartbeatPolicy` is a data record with no behavior. Binding constraint C1 is preserved.

---

### PASS — K2/K3: ResolveConnectionAsync re-negotiates on every call, unique connectId per connection (confidence: 100%)

`KucoinStreamProtocol` holds no cached token or URL field. Every call to `ResolveConnectionAsync` invokes `_bulletClient.NegotiateAsync(ct)` unconditionally. `connectId` is produced via `Guid.NewGuid().ToString("N")` inside that method body on every call. No caching pattern (lazy, volatile, field) exists. K2/K3 are satisfied.

---

### PASS — ADR-002: IStreamProtocol.ResolveConnectionAsync async seam implemented correctly (confidence: 100%)

`KucoinStreamProtocol` implements `ValueTask<StreamConnectionInfo> ResolveConnectionAsync(CancellationToken ct)` — the exact signature from ADR-002. This replaces the old static `Endpoint` + `Heartbeat` property pair. The method performs the HTTP negotiation call, constructs a fresh `HeartbeatPolicy` from server-dictated `pingInterval`/`pingTimeout`, and returns a `StreamConnectionInfo(uri, heartbeat)`. The Binance reference implementation (which returns a pre-built `StreamConnectionInfo` from the constructor) continues to work unchanged.

---

### PASS — 4-layer dependency chain: Kucoin references Core + Http only (confidence: 100%)

`CryptoExchanges.Net.Kucoin.csproj` has exactly two `ProjectReference` nodes: `CryptoExchanges.Net.Core` and `CryptoExchanges.Net.Http`. No reference to `CryptoExchanges.Net.DependencyInjection` or any sibling exchange assembly. The dependency direction invariant is satisfied.

---

### PASS — AddKucoinStreams parity with AddBinanceStreams (confidence: 100%)

`AddKucoinStreams` delegates to `StreamServiceRegistration.AddStreams<KucoinStreamOptions>` with `ExchangeId.Kucoin`, a `protocolFactory` lambda, and a `decoderRegistryFactory` lambda — exactly the same pattern as `AddBinanceStreams`. Named `kucoin` HttpClient is resolved via `IHttpClientFactory.CreateClient(KucoinClientName)` (not a typed client), satisfying Invariant 9 (no captive dependency). Keyed services (`IMapper`, `ISymbolMapper`) are resolved with `GetRequiredKeyedService`. `ValidateOnStart` is handled by the shared `StreamServiceRegistration` body.

---

### PASS — Topic map and RoutingKeyFor/Classify agreement (confidence: 100%)

`BuildTopic` produces:
- Ticker: `/market/ticker:{WIRE}`
- Trade: `/market/match:{WIRE}`
- OrderBook: `/market/level2:{WIRE}`
- Kline: `/market/candles:{WIRE}_{INTERVAL_WIRE}`

`RoutingKeyFor` returns `BuildTopic(request)` directly. `Classify` reads `"topic"` from an inbound `"message"` frame and returns it verbatim as the routing key. Both sides share exactly the same venue-native topic string. Subscribe-time registration and receive-time routing will agree. The keyspace contract from `IStreamProtocol` is met.

---

### PASS — SSRF guard on bullet-public endpoint (confidence: 100%)

`ValidateWsEndpoint` enforces that the negotiated endpoint URI (a) parses as an absolute URI, (b) uses the `wss` scheme, and (c) has a host that is exactly `kucoin.com` or ends with `.kucoin.com`. This prevents a compromised negotiation response from redirecting the WebSocket connection to an attacker-controlled host. The guard fires before the URI is used to construct `StreamConnectionInfo`.

---

### PASS — KucoinStreamDecoders is a static factory, not a static utility with swappable behavior (confidence: 95%)

`KucoinStreamDecoders` is `internal static` and exposes only `Build(IMapper, ISymbolMapper)`. It is a pure factory that produces a `StreamDecoderRegistry`; the decoders themselves are closures capturing the injected `IMapper` and `ISymbolMapper`. The static class holds no mutable state and does not implement any swappable interface. This is structurally analogous to `BinanceStreamDecoders` and matches the pattern described in `StreamServiceRegistration`. Invariant 11 (interfaces for swappable behavior) is not violated here.

---

### CONCERN — KucoinStreamOptions.RestBaseUrl is public but never consumed (confidence: 90%, non-blocking)

**File**: `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamOptions.cs:12`

`KucoinStreamOptions.RestBaseUrl` is a `public` settable property that documents itself as the URL for the bullet-public endpoint. However, `KucoinStreamProtocol` never reads `KucoinStreamOptions`; neither does `KucoinBulletPublicClient`. The actual REST base address is fixed by `AddKucoinExchange` when it registers the named `kucoin` HttpClient. `KucoinStreamOptions` is passed through `StreamServiceRegistration.AddStreams<KucoinStreamOptions>` solely to enable `ValidateOnStart` on the options type — but since there are no `[Required]` or `[Range]` attributes and `RestBaseUrl` has a hardcoded default, `ValidateOnStart` is a no-op.

The practical risks: (1) a consumer sets `KucoinStreamOptions.RestBaseUrl` expecting to change the negotiation endpoint, but the named `kucoin` HttpClient base address is unaffected — silent misconfiguration. (2) `RestBaseUrl` is part of the public API surface of a NuGet package with no semantic effect. Removing it later is a breaking change.

**Recommendation**: Either (a) wire `RestBaseUrl` into the bullet-public HTTP call (e.g. have the `protocolFactory` lambda in `AddKucoinStreams` resolve `KucoinStreamOptions` and pass the URL to `KucoinHttpClient`), or (b) remove `RestBaseUrl` entirely and keep `KucoinStreamOptions` as an empty placeholder for future options, or (c) add a `[Required]` / `[MinLength(1)]` attribute and actually use it, so it earns its place on the public surface. This is non-blocking for the current task since the SSRF guard and named-client pattern both work correctly at runtime, but the dead public property should be resolved before the next milestone's release.

---

### CONCERN — Two top-level types per file in BulletPublicDto.cs and StreamDepthDto.cs (confidence: 95%, non-blocking)

**Files**:
- `src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/BulletPublicDto.cs` — `BulletPublicDto` + `InstanceServerDto`
- `src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamDepthDto.cs` — `StreamDepthDto` + `DepthChangesDto`

The project convention (CLAUDE.md: "One type per file") is violated in both cases. The second types (`InstanceServerDto`, `DepthChangesDto`) are tightly coupled nested-payload types, which may be why they were co-located — but the convention is unconditional. The same pattern exists in the Binance and other exchange packages for similarly-paired nested DTOs (the task manifest notes this is "known"), so this is a pre-existing pattern issue being cloned rather than a new violation introduced without precedent.

**Recommendation**: Split each file: `InstanceServerDto.cs` alongside `BulletPublicDto.cs`; `DepthChangesDto.cs` alongside `StreamDepthDto.cs`. This is consistent with how the convention is applied to all other DTO types in the project. Non-blocking for the current task, but cloning this pattern to each new exchange makes the violation grow proportionally.

**Pattern reference**: Every other DTO under `src/CryptoExchanges.Net.Kucoin/Dtos/` follows the one-type-per-file rule.

---

### CONCERN — IKucoinBulletPublicClient and KucoinBulletPublicClient co-located in one file (confidence: 85%, non-blocking)

**File**: `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinBulletPublicClient.cs`

`IKucoinBulletPublicClient` (interface) and `KucoinBulletPublicClient` (implementation) are both defined in `KucoinBulletPublicClient.cs`. The one-type-per-file convention applies to interfaces as well as classes. Severity is low given both types are `internal` and tightly coupled, but the convention violation is the same as the DTO case above.

**Recommendation**: Move `IKucoinBulletPublicClient` to its own file (`IKucoinBulletPublicClient.cs`). Non-blocking.

---

## Build Verification

`dotnet build --configuration Release -warnaserror` on the full solution: **Build succeeded. 0 Warning(s). 0 Error(s).** All downstream assemblies (DI, tests, samples) compiled cleanly.

---

## Summary

| Check | Result | Notes |
|---|---|---|
| K1: Http layer not touched | PASS | No Http files modified |
| C1: No timers/threads in protocol | PASS | Confirmed via grep |
| K2/K3: Token re-negotiated on every call | PASS | No caching field; Guid per call |
| ADR-002: ResolveConnectionAsync seam | PASS | Correct ValueTask<StreamConnectionInfo> signature |
| 4-layer dependency chain | PASS | Core + Http only in csproj |
| AddKucoinStreams parity | PASS | Mirrors AddBinanceStreams pattern |
| Topic map + routing key agreement | PASS | RoutingKeyFor/Classify share same BuildTopic |
| SSRF guard on WS endpoint | PASS | wss:// + *.kucoin.com enforced |
| RestBaseUrl never consumed | CONCERN | Public property with no semantic effect (confidence: 90%) |
| Two types per file (DTOs) | CONCERN | Pre-existing pattern cloned (confidence: 95%) |
| Two types per file (client+interface) | CONCERN | IKucoinBulletPublicClient co-located (confidence: 85%) |
| Build clean | PASS | 0 warnings, 0 errors |

All three concerns are non-blocking. No hard constraints (K1, C1, K2/K3, ADR-002, dependency chain) are violated. The implementation is architecturally sound and ready to proceed.
