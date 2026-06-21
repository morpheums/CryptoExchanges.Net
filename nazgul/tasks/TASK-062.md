---
id: TASK-062
status: DONE
depends_on: [TASK-058, TASK-060, TASK-061]
---
# TASK-062: `KucoinStreamProtocol` + bullet-public negotiation + 4 decoders + `AddKucoinStreams`

## Metadata
- **ID**: TASK-062
- **Group**: 5
- **Status**: (see `status:` in the frontmatter block at the top — canonical, read by scripts/lib/structured-state.sh)
- **Depends on**: TASK-058, TASK-060, TASK-061
- **Delegates to**: none
- **Files modified**: [src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs, src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamDecoders.cs, src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamOptions.cs, src/CryptoExchanges.Net.Kucoin/Streaming/KucoinBulletPublicClient.cs, src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/BulletPublicDto.cs, src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamTickerDto.cs, src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamTradeDto.cs, src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamDepthDto.cs, src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamKlineDto.cs, src/CryptoExchanges.Net.Kucoin/StreamServiceCollectionExtensions.cs, tests/CryptoExchanges.Net.Kucoin.Tests.Unit/Streaming/KucoinStreamProtocolTests.cs, tests/CryptoExchanges.Net.Kucoin.Tests.Unit/Streaming/KucoinStreamDecodeTests.cs, tests/CryptoExchanges.Net.Kucoin.Tests.Unit/Streaming/KucoinStreamOptionsWiringTests.cs]
- **Wave**: 6
- **Traces to**: PRD-FEAT-006 AC-3, AC-4; TRD-FEAT-006 §"WebSocket Streaming — KuCoin Protocol", §"Data Flow (Streaming)"; FEAT-006 spec §"WebSocket streaming (public)", §"Build approach" step 7; TEST-PLAN-FEAT-006 §6, §7
- **Created at**: 2026-06-20T19:00:00Z
- **Claimed at**: 2026-06-21T00:00:00Z
- **Base SHA**: fb8a8855b3f2901ffe7c07614c11273787203280
- **Implemented at**: 2026-06-21T00:30:00Z
- **Completed at**: 2026-06-21T13:15:00Z
- **Blocked at**:
- **Retry count**: 1/3
- **Test failures**: 0

## Description

Implement KuCoin's public WebSocket protocol on the generalized seam, cloning the Binance streaming
package and adding KuCoin's token-negotiated connection. `Core.Models` + DeltaMapper legitimately live
here (K1 is Http-only). One type per file. Depends on the seam from TASK-061, the symbol mapper/profiles
from TASK-058, and the keyed DI registration from TASK-060 (for `AddKucoinStreams` to mirror).

Create:
- **`Dtos/Streaming/BulletPublicDto.cs`** — the `POST /api/v1/bullet-public` response shape: `token` +
  `instanceServers[{endpoint, pingInterval, pingTimeout}]` (under the `ResponseDto`/`data` envelope).
- **`Dtos/Streaming/StreamTickerDto.cs` / `StreamTradeDto.cs` / `StreamDepthDto.cs` /
  `StreamKlineDto.cs`** — the four KuCoin push frame shapes (the `{"type":"message","topic":...,"data":{...}}`
  inner `data` payloads; distinct from REST DTOs).
- **`Streaming/KucoinBulletPublicClient.cs`** — `NegotiateAsync(ct)` POSTs `/api/v1/bullet-public`
  (unauthenticated) via the resilient HTTP client → `BulletPublicDto`. Injectable interface so tests
  fake it (no network).
- **`Streaming/KucoinStreamProtocol.cs`** (`internal sealed : IStreamProtocol`):
  `ResolveConnectionAsync(ct)` → calls `KucoinBulletPublicClient.NegotiateAsync`, picks the first
  instance server, appends `?token={token}&connectId={Guid:N}`, returns `StreamConnectionInfo(uri,
  new HeartbeatPolicy(ClientPing, pingInterval, pingTimeout, jsonPingPayload, PingFormat.Json))` with the
  KuCoin ping payload `{"id":"<ts>","type":"ping"}`. `BuildSubscribe`/`BuildUnsubscribe` produce the
  KuCoin JSON (`type:subscribe|unsubscribe`, `topic`, `privateChannel:false`, `response:true`). Topic
  map: Ticker `/market/ticker:{WIRE}`, Trade `/market/match:{WIRE}`, OrderBook `/market/level2:{WIRE}`,
  Kline `/market/candles:{WIRE}_{INTERVAL_WIRE}`. `RoutingKeyFor` agrees with `Classify`. `Classify`
  reads `"type"`: `message`→Data (routing key = `topic`), `ack`→Ack, `pong`→Pong, `error`/unknown→Error.
  Pure data + classification — NO timers/threads (C1).
- **`Streaming/KucoinStreamDecoders.cs`** — 4 decode closures (`Func<ReadOnlyMemory<byte>, object>`):
  Ticker via DeltaMapper (`StreamTickerDto`→`Ticker`), Trade/OrderBook/Kline hand-mapped (matching the
  Binance streaming convention), reusing the keyed `ISymbolMapper` for wire→domain symbol. K1: DeltaMapper
  used HERE, in the Kucoin package.
- **`Streaming/KucoinStreamOptions.cs`** — `RestBaseUrl` (bullet-public host) + optional stream-ping
  override; validatable via `ValidateOnStart`.
- **`StreamServiceCollectionExtensions.cs`** — `AddKucoinStreams()` (~5–10 lines) delegating to
  `StreamServiceRegistration.AddStreams<KucoinStreamOptions>` supplying the protocol + decoder-registry
  factories, registering the keyed `IStreamClient` for `ExchangeId.Kucoin`. Mirror
  `BinanceStreamServiceCollectionExtensions`/`AddBinanceStreams`.

Tests (`Streaming/` in the Kucoin unit project), no network:
- Protocol: subscribe topic for each StreamKind (Ticker/Trade/OrderBook/Kline incl. `BTC-USDT_1min`);
  unsubscribe type; `RoutingKeyFor`==`Classify` round-trip; `Classify` for message/ack/pong/error;
  `ResolveConnectionAsync` with a FAKE `KucoinBulletPublicClient` returning a fixture `BulletPublicDto`
  → `StreamConnectionInfo` has the token query param + heartbeat from pingInterval ms.
- Decoders: each of the 4 with a JSON bytes fixture → correct `Core.Models` (ticker price/volume, trade
  price/qty/side/ts, orderbook bids/asks, candlestick OHLCV + interval).

## Acceptance Criteria
- [x] `KucoinStreamProtocol : IStreamProtocol` (`internal sealed`, `ResolveConnectionAsync` via fake-able `KucoinBulletPublicClient` → token+connectId URL + server-dictated `HeartbeatPolicy(ClientPing, JSON ping)`, KuCoin subscribe/unsubscribe wire, 4-topic map, `Classify` by `type`, `RoutingKeyFor`==`Classify`, no timers — C1) + 4 streaming DTOs + `BulletPublicDto` + `KucoinStreamOptions` exist, one type per file, full XML docs.
- [x] `KucoinStreamDecoders` registers 4 decode closures (Ticker via DeltaMapper; Trade/OrderBook/Kline hand-mapped) reusing the keyed `ISymbolMapper` → boxed `Core.Models`; `AddKucoinStreams()` registers the keyed `IStreamClient` for `ExchangeId.Kucoin`, mirroring `AddBinanceStreams`.
- [x] `Streaming/` unit tests assert subscribe/unsubscribe wire + 4-topic map + `Classify` (message/ack/pong/error) + `RoutingKeyFor` round-trip + `ResolveConnectionAsync` (fake bullet-public → token URL + heartbeat) + 4 decoder `Core.Models` outputs — ALL NO network; solution builds 0W/0E; existing non-integration suite stays green.

## Pattern Reference
- Streaming package to clone: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs`, `BinanceStreamDecoders.cs`, `BinanceStreamOptions.cs` + `Dtos/Streaming/Stream*Dto.cs`.
- Registration delegator: `src/CryptoExchanges.Net.Binance/StreamServiceCollectionExtensions.cs` (`AddBinanceStreams` → `AddStreams<TOptions>`).
- Seam (post-TASK-061): `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs` (`ResolveConnectionAsync`) + `StreamConnectionInfo.cs` + `HeartbeatPolicy.cs` + `PingFormat.cs` + `StreamKind.cs`.
- DeltaMapper profile + `ISymbolMapper` reuse: `src/CryptoExchanges.Net.Kucoin/Mapping/KucoinMappingProfiles.cs` (TASK-058). Bullet-public response shape: TRD-FEAT-006 §"Bullet-public negotiation". Decoder/protocol tests: `tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/`.

## File Scope

**Creates**:
- src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs
- src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamDecoders.cs
- src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamOptions.cs
- src/CryptoExchanges.Net.Kucoin/Streaming/KucoinBulletPublicClient.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/BulletPublicDto.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamTickerDto.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamTradeDto.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamDepthDto.cs
- src/CryptoExchanges.Net.Kucoin/Dtos/Streaming/StreamKlineDto.cs
- src/CryptoExchanges.Net.Kucoin/StreamServiceCollectionExtensions.cs
- tests/CryptoExchanges.Net.Kucoin.Tests.Unit/Streaming/KucoinStreamProtocolTests.cs
- tests/CryptoExchanges.Net.Kucoin.Tests.Unit/Streaming/KucoinStreamDecodeTests.cs
- tests/CryptoExchanges.Net.Kucoin.Tests.Unit/Streaming/KucoinStreamOptionsWiringTests.cs (cycle-1 fix)

**Modifies**:
- (none — additive; `KucoinMappingProfiles` extended only if a WS DTO needs a new CreateMap, but prefer reuse)

## Traceability
- **PRD Acceptance Criteria**: AC-3 (4 public streams → Core.Models), AC-4 (reconnect re-negotiates token — `ResolveConnectionAsync` per connect), AC-7 (no-network tests)
- **TRD Component**: §"WebSocket Streaming — KuCoin Protocol", §"Data Flow (Streaming)"
- **ADR Reference**: ADR-002 (KuCoin implements `ResolveConnectionAsync`); FEAT-006 spec §"WebSocket streaming"; K1 (DeltaMapper in Kucoin pkg, not Http); C1 (protocol describes heartbeat)

## Commits

- `af4d08a` — feat(FEAT-006): TASK-062 — KucoinStreamProtocol + bullet-public negotiation + 4 decoders + AddKucoinStreams
- `d6988f7` — feat(FEAT-006): simplify TASK-062 (6 fixes: XML-doc corrections + per-frame decoder allocation removal)
- `2039654` — feat(FEAT-006): TASK-062 DONE — RestBaseUrl wiring fix (cycle 2 review 4/4)

## Implementation Log

### Review cycle 1 — api-reviewer REJECT@98% blocking fix (RestBaseUrl silently ignored)

**Finding:** `KucoinStreamOptions.RestBaseUrl` was a public, caller-configurable option that the
`protocolFactory` in `StreamServiceCollectionExtensions.cs` never read. The bullet-public client was
built from the named "kucoin" HttpClient whose `BaseAddress` is fixed by `AddKucoinExchange`, so a
consumer setting `RestBaseUrl` (e.g. for sandbox) got no effect.

**Fix (surgical, RestBaseUrl wiring only):**
- `src/CryptoExchanges.Net.Kucoin/StreamServiceCollectionExtensions.cs` — `AddKucoinStreams`
  `protocolFactory` now resolves `sp.GetRequiredService<KucoinStreamOptions>()` and overrides the
  per-call HttpClient's `BaseAddress` with the configured `RestBaseUrl` before wrapping it:
  ```csharp
  var options = sp.GetRequiredService<KucoinStreamOptions>();
  ArgumentException.ThrowIfNullOrWhiteSpace(options.RestBaseUrl);   // LR-001
  if (!Uri.TryCreate(options.RestBaseUrl, UriKind.Absolute, out var baseUri))
      throw new ArgumentException(
          $"{nameof(KucoinStreamOptions)}.{nameof(KucoinStreamOptions.RestBaseUrl)} must be a well-formed absolute URI. Got: '{options.RestBaseUrl}'.");
  var httpClient = httpClientFactory.CreateClient(KucoinClientName);
  httpClient.BaseAddress = baseUri;
  ```
  `IHttpClientFactory.CreateClient` returns a fresh `HttpClient` per call, so the override only affects
  the bullet-public negotiation and never mutates the shared REST client. The default
  (`https://api.kucoin.com`) behaviour is unchanged when `RestBaseUrl` is not overridden.
- **LR-001 applied:** guarded the caller-configurable `RestBaseUrl` at the consumption point with
  `ArgumentException.ThrowIfNullOrWhiteSpace` plus an absolute-URI well-formedness check that throws a
  clear `ArgumentException` otherwise. (CA2208 forced the message to avoid a non-parameter `paramName`,
  so the URI-format throw uses the parameterless `ArgumentException(message)` overload with the property
  name embedded in the message; the whitespace guard keeps its `CallerArgumentExpression`.)

**New test file:** `tests/CryptoExchanges.Net.Kucoin.Tests.Unit/Streaming/KucoinStreamOptionsWiringTests.cs`
(no-network, capturing `HttpMessageHandler`):
- `AddKucoinStreams_CustomRestBaseUrl_IsHostUsedForBulletPublicNegotiation` — registers
  `AddKucoinExchange + AddKucoinStreams(o => o.RestBaseUrl = "https://sandbox-api.kucoin.com")`, replaces
  the named "kucoin" primary handler with a capturing stub, resolves the keyed `IStreamClient`, drives
  the real bullet-public `NegotiateAsync`, and asserts the captured outgoing request URI host is
  `sandbox-api.kucoin.com` at path `/api/v1/bullet-public`.
- `AddKucoinStreams_DefaultRestBaseUrl_NegotiatesAgainstProductionHost` — default path still targets
  `api.kucoin.com`.
- `AddKucoinStreams_WhitespaceRestBaseUrl_FailsFast` and
  `AddKucoinStreams_RelativeRestBaseUrl_FailsFast` — assert the LR-001 guards throw `ArgumentException`.

Did NOT address the non-blocking concerns (one-type-per-file splits, ValueKind guards, OrderBook bounds
checks) per the cycle-1 scope. Left `src/CryptoExchanges.Net.Http/` untouched (K1).

**Build:** `dotnet build CryptoExchanges.Net.sln` → 0 Warning(s), 0 Error(s).
**Tests:** `dotnet test --filter 'Category!=Integration'` → all projects green; Kucoin.Tests.Unit
200/200 (4 new), Http.Tests.Unit 87/87 (no flake this run).

## Review Results

**Cycle 1** (4 reviewers): architect APPROVE, code APPROVE, security APPROVE, api CHANGES_REQUESTED.
- Blocking (api-reviewer REJECT@98%): `KucoinStreamOptions.RestBaseUrl` public option silently ignored
  by `protocolFactory`. Fix-First auto-remediation: implementer wired `RestBaseUrl` into the bullet-public
  HttpClient `BaseAddress` (LR-001 guards + absolute-URI validation) + 4 no-network wiring tests.
- Security SSRF (previously deferred to this task): RESOLVED — `KucoinStreamProtocol.ValidateWsEndpoint`
  enforces `wss://` scheme + `*.kucoin.com` host allowlist before URI construction, with two rejection
  tests. security-reviewer APPROVE@98%.
- Non-blocking (carried, NOT gating): one-type-per-file in BulletPublicDto.cs/StreamDepthDto.cs/
  KucoinBulletPublicClient.cs (cloned pattern, cosmetic); `Classify` GetString ValueKind guard
  (pre-existing Binance pattern, 70%); OrderBook decoder `b[0]/a[0]` bounds (security CONCERN@75%,
  depends on engine try/catch); duplicate "kucoin" client-name constant (90%).

**Cycle 2** (api-reviewer re-review): APPROVE@99% — blocking finding fully resolved, no new defects.

**Gate (require_all_approve=true): APPROVED 4/4.**

Pre-checks (final): `dotnet build` 0W/0E; `dotnet test --filter 'Category!=Integration'` green
(Kucoin.Tests.Unit 200/200, Http.Tests.Unit 87/87 — no parallel-run flake this run).
Simplify pass (Step 0): commit `d6988f7` — 6 fixes applied, tests green.
Review artifacts: `nazgul/reviews/TASK-062/{architect,code,security,api}-reviewer.md`, `consolidated-feedback.md`.
