---
reviewer: security-reviewer
task: TASK-061
verdict: APPROVE
confidence: 97
---
# Security Review — TASK-061

## Verdict: APPROVE

---

## Findings

### Finding 1: Endpoint URI integrity — Binance implementation is safe
- **Severity**: N/A (no issue)
- **Confidence**: 98
- **File**: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs:34`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. `StreamBaseUrl` originates from `BinanceStreamOptions`, a DI-resolved configuration object set at startup, not at runtime from user input. The URI is constructed once in the constructor and cached in `_connectionInfo`. No user-supplied value reaches `new Uri(...)` here.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamOptions.cs:12` (hardcoded default `wss://stream.binance.com:9443`)

---

### Finding 2: URI scheme enforcement — downstream guard present
- **Severity**: N/A (no issue)
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Http/Streaming/ClientWebSocketConnection.cs:69-74`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. `ClientWebSocketConnection.ConnectAsync` throws `ArgumentException` if the URI scheme is not `ws` or `wss`. This provides a hard enforcement gate even for future `ResolveConnectionAsync` implementations that perform token negotiation and construct a dynamic URI.
- **Pattern reference**: `src/CryptoExchanges.Net.Http/Streaming/ClientWebSocketConnection.cs:69-74`

---

### Finding 3: Future SSRF risk from I/O-capable ResolveConnectionAsync (KuCoin token negotiation)
- **Severity**: LOW
- **Confidence**: 50
- **File**: `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs:29-34`
- **Category**: Security
- **Verdict**: CONCERN (non-blocking, confidence < 80)
- **Issue**: The interface contract explicitly documents that implementations may perform I/O (e.g., a bullet-public HTTP call for KuCoin). When that HTTP response includes a server-provided URI (e.g., `wss://ws-api.kucoin.com:443/endpoint?token=<token>`), the token is embedded in the URI and passed to `ConnectAsync`. If the HTTP response is compromised (e.g., via a MITM or DNS substitution), an attacker could provide a URI pointing to an attacker-controlled host. The scheme check in `ClientWebSocketConnection` prevents non-WebSocket schemes but does not validate the hostname.
- **Fix**: When the KuCoin protocol implementation is written, validate that the resolved `Endpoint` hostname matches the expected KuCoin host before connecting (or document explicitly that TLS certificate pinning is the trust anchor). No action required on this task — no KuCoin implementation is present.
- **Pattern reference**: `src/CryptoExchanges.Net.Http/Streaming/ClientWebSocketConnection.cs:69-74` (scheme check as a model for future host validation)

---

### Finding 4: Cancellation token propagation — correct
- **Severity**: N/A (no issue)
- **Confidence**: 98
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:285`, `:506`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. Both `ResolveConnectionAsync` call sites pass the appropriate token:
  - `OpenSocketAsync` (line 285): passes the caller-supplied `ct` (comes from `SubscribeAsync` which accepts a `CancellationToken`).
  - `ReconnectCoreAsync` (line 506): passes `_disposeCts.Token`, which is cancelled when `DisposeAsync` is called. An `OperationCanceledException` from resolution propagates to the outer try/catch at line 511, which logs the failure and continues the retry loop (or exits on `_disposeCts` cancellation). No partial state is left — `newSocket` is null and no `StartPump` call occurs.

---

### Finding 5: Gate acquire/release around ResolveConnectionAsync — correct
- **Severity**: N/A (no issue)
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs:499-558`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. In `ReconnectCoreAsync`, `_gate.WaitAsync` is acquired at line 499 and `_gate.Release()` is in the `finally` block at line 557 — wrapping both `ResolveConnectionAsync` and `ConnectAsync`. If `ResolveConnectionAsync` throws (including `OperationCanceledException`), the `finally` block still releases the gate. `OpenSocketAsync` runs inside the `SubscribeAsync` gate guard, which also uses try/finally. No deadlock or gate leak path exists.

---

### Finding 6: Signing pipeline untouched
- **Severity**: N/A (no issue)
- **Confidence**: 100
- **File**: diff.patch (all changed files listed)
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. Git inspection of the three TASK-061 commits confirms that no files under `src/CryptoExchanges.Net.Binance/Auth/` or `src/CryptoExchanges.Net.Binance/Resilience/` (signing handler, signing request, signature service) were modified. The signing pipeline is completely untouched.

---

### Finding 7: Credential safety — no secrets in new code paths
- **Severity**: N/A (no issue)
- **Confidence**: 100
- **File**: All changed files
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None. `StreamConnectionInfo` holds only `Uri` and `HeartbeatPolicy` — no `ApiKey`, no `SecretKey`, no auth headers. `BinanceStreamProtocol` stores only the pre-built `StreamConnectionInfo` and an `int _nextId`. No credential field, no logging call referencing credentials, no `ToString()` override that could leak secrets. The `StreamEngine` has no credential awareness whatsoever.

---

### Finding 8: JSON deserialization safety in Classify
- **Severity**: N/A (no issue — pre-existing, engine protects it)
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs:79-80`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: `Classify` calls `JsonDocument.ParseValue` without a local `try/catch` for `JsonException`. However, this is unchanged from the pre-TASK-061 code. The caller — `PumpLoopAsync` in `StreamEngine` — wraps the entire `Classify` + dispatch block in a `catch (Exception ex)` (line 406) that logs and continues, so a `JsonException` on a malformed frame will log `DispatchException` and drop the frame without killing the pump. The pump is not restarted and no reconnect is triggered.

---

## Summary

- PASS: Endpoint URI integrity — Binance constructs the URI from static DI configuration, not runtime user input. Cached in constructor, not re-evaluated per call.
- PASS: URI scheme enforcement — `ClientWebSocketConnection.ConnectAsync` enforces `ws`/`wss` scheme, blocking non-WebSocket redirects.
- PASS: Cancellation token propagation — `ct` correctly flows through both `ResolveConnectionAsync` call sites; cancellation leaves no partial state.
- PASS: Gate acquire/release — `_gate` is acquired before and released in `finally` after every `ResolveConnectionAsync` call in `ReconnectCoreAsync`; no deadlock or gate leak path.
- PASS: Signing pipeline untouched — git history confirms zero changes to Auth/ and Resilience/ signing files.
- PASS: Credential safety — new types (`StreamConnectionInfo`, updated `BinanceStreamProtocol`) contain no credential fields, no logging of secrets, no `JsonInclude` attributes, no serialization exposure.
- PASS: JSON deserialization safety — `JsonException` from `Classify` is caught by the engine's outer pump dispatch guard; pump survives malformed frames.
- CONCERN: Future SSRF from token-negotiated URI (confidence: 50/100, non-blocking) — when KuCoin bullet-public is implemented, the resolved URI hostname should be validated against the expected KuCoin host, or TLS pinning documented as the trust anchor. No action required on this task.
