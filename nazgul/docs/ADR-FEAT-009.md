# ADR — FEAT-009: Architecture Decisions for Bybit/OKX/Bitget Streaming

## ADR-009-001: Clone the existing streaming pattern; no new engine

**Status:** Accepted

**Context:**
A verified shared engine (`StreamEngine` + `IStreamProtocol` + `StreamDecoderRegistry` +
`StreamServiceRegistration.AddStreams<TOptions>()`) already supports Binance and KuCoin,
including the FEAT-008 multi-symbol pacing fix. Introducing a new transport or abstraction
layer would increase test surface and risk regressions in the working clients.

**Decision:**
Each new exchange supplies only its five variation points (options class, `IStreamProtocol`,
`XxxStreamDecoders`, wire DTOs, `AddXxxStreams()` extension). The shared engine is
consumed unchanged.

**Alternatives rejected:**
- New per-exchange WebSocket client class: duplicates all reconnect, heartbeat, and
  channel machinery.
- New generic adapter with reflection: violates the explicit-mapping house rule and K1.

---

## ADR-009-002: Static endpoint URI for Bybit and OKX; static endpoint for Bitget

**Status:** Accepted

**Context:**
KuCoin requires a bullet-public HTTP round-trip to negotiate a short-lived WS token per
connection. Bybit v5, OKX v5, and Bitget v2 all use static public WS URLs — no token
negotiation is needed for public spot streams.

**Decision:**
`ResolveConnectionAsync` for all three venues caches the `StreamConnectionInfo` in the
constructor and returns it via `ValueTask.FromResult`. No I/O on reconnect.

**Alternatives rejected:**
- Forcing a token-negotiation pattern: unnecessary complexity; these venues don't require it.

---

## ADR-009-003: Heartbeat policy per venue

**Status:** Accepted

**Context:**
The engine distinguishes two heartbeat directions: the venue sends pings and the client
auto-pongs (handled by `ClientWebSocket` keep-alive), vs. the client sends periodic ping
frames (text or JSON) and watches for a pong response.

| Exchange | Direction | Notes |
|----------|-----------|-------|
| Binance | `ServerPingClientPong` | .NET auto-pong on control-frame Ping |
| KuCoin | `ClientPing`, `PingFormat.Json` | JSON `{"id":"<ts>","type":"ping"}` every 18 s |
| Bybit | `ServerPingClientPong` (TBC) | 20 s server ping; implementor confirms from v5 docs |
| OKX | `ClientPing`, `PingFormat.Text` | Text `"ping"` every 25 s; pong is text `"pong"` |
| Bitget | TBC | Implementor confirms direction from v2 docs |

**Decision:**
Each protocol sets the `HeartbeatPolicy` inside `ResolveConnectionAsync`. The engine
executes it (C1). The policy is set based on the public documentation for each venue,
confirmed by the implementor before writing the protocol class.

---

## ADR-009-004: `MinOutboundInterval` set to 100 ms for all three venues

**Status:** Accepted (subject to revision per actual docs)

**Context:**
FEAT-008 established that Binance's ~5 msg/s limit requires 200 ms pacing. KuCoin uses
100 ms. Bybit, OKX, and Bitget each publish connection-level subscribe-rate limits; their
public docs indicate limits are less restrictive than Binance but the exact values need
per-venue confirmation.

**Decision:**
Start at `MinOutboundInterval = TimeSpan.FromMilliseconds(100)` for all three venues
(same as KuCoin). If the implementor confirms a more restrictive or lenient limit from
vendor docs, override per venue. Document the confirmed value in the options class summary.

---

## ADR-009-005: One PR per exchange; strict merge order

**Status:** Accepted

**Context:**
Each exchange is self-contained (separate project, separate test projects, separate DI
extension). Merging one at a time keeps PR diffs reviewable, allows the CI gate to
validate each exchange independently, and isolates any venue-specific issue.

**Decision:**
Bybit is implemented and merged to main first. OKX branch is cut from main after the
Bybit PR merges. Bitget follows the same pattern after OKX. The planner tracks this as
three separate task groups with a hard sequencing dependency.

---

## ADR-009-006: Routing key convention per venue

**Status:** Accepted

**Context:**
`IStreamProtocol.RoutingKeyFor` (subscribe side) and `Classify` (receive side) must
agree on the routing key for the engine to route frames to the right subscription.

| Exchange | Routing key format |
|----------|--------------------|
| Binance | `<symbol_lower>@<kind>[_interval]` e.g. `btcusdt@ticker` |
| KuCoin | Full topic string e.g. `/market/snapshot:BTC-USDT` |
| Bybit | Topic field verbatim e.g. `tickers.BTCUSDT` |
| OKX | `<channel>:<instId>` e.g. `tickers:BTC-USDT` |
| Bitget | `<channel>:<instId>` e.g. `ticker:BTCUSDT` |

**Decision:**
Each protocol single-sources the routing key in a private `BuildTopic`/`BuildToken`
helper used by both `RoutingKeyFor` and `Classify`, exactly as Binance's
`BuildStreamToken` is used by both methods.
