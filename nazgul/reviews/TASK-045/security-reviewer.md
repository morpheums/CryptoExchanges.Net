# Security Review — TASK-045

## Verdict: APPROVED

## Findings
| # | File | Line | Severity | Confidence | Status | Description |
|---|------|------|----------|------------|--------|-------------|
| 1 | ClientWebSocketConnection.cs | 49-72 | MEDIUM | 55 | CONCERN | No maximum buffer size limit on unbounded multi-frame receive loop |
| 2 | ClientWebSocketConnection.cs | 26-30 | LOW | 50 | CONCERN | No WebSocket scheme validation (ws:// or wss://) before ConnectAsync |
| 3 | StreamServiceRegistration.cs | 94-112 | LOW | 30 | PASS | Keyed singleton isolation: each exchange resolves its own ISymbolMapper |

---

## Detailed Findings

### Finding 1: No maximum receive buffer size limit (unbounded multi-frame accumulation)
- **Severity**: MEDIUM
- **Confidence**: 55
- **File**: `ClientWebSocketConnection.cs:49-72`
- **Category**: Security
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `ReceiveAsync` accumulates frames into an unbounded `MemoryStream`. A pathological or malicious server could send a single logical WebSocket message fragmented into arbitrarily many frames, each up to the 8 192-byte chunk size, causing unbounded heap growth. There is no cap on `ms.Length` before writing the next chunk.
- **Fix**: Add a size guard inside the loop, e.g.: `if (ms.Length + result.Count > MaxMessageBytes) throw new InvalidOperationException($"WebSocket message exceeded {MaxMessageBytes} bytes");` with a `MaxMessageBytes` constant (e.g. 4 MiB for market-data streams, matching typical exchange limits). This bounds the worst-case allocation to one message per connection rather than the full heap.
- **Pattern reference**: This is a standard WebSocket hardening concern; no existing pattern in this codebase applies directly. Typical exchange stream messages are well under 64 KiB — a 4 MiB cap is a safe operational bound.

---

### Finding 2: No WebSocket URI scheme validation in ConnectAsync
- **Severity**: LOW
- **Confidence**: 50
- **File**: `ClientWebSocketConnection.cs:26-30`
- **Category**: Security
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `ConnectAsync` accepts any `Uri` and passes it directly to `ClientWebSocket.ConnectAsync` without validating that the scheme is `ws` or `wss`. If a caller (or misconfigured `IStreamProtocol.Endpoint`) supplies an `http://` or `file://` URI, the underlying `ClientWebSocket` will throw a `WebSocketException` with implementation-detail text that may surface to callers. The rejection is not the concern — the error message content and the absence of an early, clear guard is.
- **Fix**: Add scheme validation before the delegate call: `if (uri.Scheme is not ("ws" or "wss")) throw new ArgumentException($"URI scheme must be ws or wss, got '{uri.Scheme}'.", nameof(uri));` This produces a clear, early `ArgumentException` instead of a late `WebSocketException` with internal state.
- **Pattern reference**: Null/whitespace guards at method boundaries — `ClientWebSocketConnection.cs:28` and `ClientWebSocketConnection.cs:35` — show the existing validation style; scheme validation follows the same pattern.

---

### Finding 3: Keyed singleton cross-exchange isolation
- **Severity**: LOW
- **Confidence**: 30
- **File**: `StreamServiceRegistration.cs:87-112`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: Reviewed for the risk of one exchange resolving another exchange's keyed `ISymbolMapper`. The implementation uses `GetRequiredKeyedService<ISymbolMapper>(exchangeId)` with the exchange-specific key at line 110, matching the per-exchange registration at line 87. The `TryAddKeyedSingleton` fallback at line 87 throws `InvalidOperationException` rather than silently returning null or a wrong instance. Cross-exchange resolution is not possible given .NET keyed DI semantics.
- **Fix**: No action required.
- **Pattern reference**: N/A — PASS.

---

## Credential and Secret Checks

**grep result (api.key|secret|password|token|credential — case-insensitive):**
- All matches in `Streaming/` are `CancellationToken ct` parameter names and a single doc-comment reference to "credentials" in `StreamServiceRegistration.cs:39` (the phrase "resolve its own keyed options/credentials" in XML documentation only).
- No API keys, secret key fields, credential storage, or serializable secret properties exist anywhere in the `Streaming/` directory.

**Credential safety**: PASS — `StreamClient` and `ClientWebSocketConnection` carry no credential fields. `StreamEngineOptions` contains only behavioral tuning (channel capacity, backoff timing, idle-close delay) with no secret fields. This is consistent with the stated design: streaming endpoints are public market data requiring no authentication headers.

**Signing pipeline**: Not applicable — streaming is unauthenticated public data. No `BinanceSigningRequest`, `timestamp`, or `signature` parameters are involved. PASS.

**Query string safety**: Not applicable — WebSocket connections use URI-level endpoint configuration owned by `IStreamProtocol.Endpoint`, not user-constructed query strings in this layer. PASS.

**JSON deserialization**: `ClientWebSocketConnection` and `StreamDecoderRegistry` do not call `JsonDocument.Parse` directly; deserialization is deferred to exchange-package closures via the opaque `Func<ReadOnlyMemory<byte>, object>` registered per `StreamKind`. This class is not in scope for JSON exception handling. PASS.

**Input validation**: All public boundaries call `ArgumentNullException.ThrowIfNull` or `ArgumentException.ThrowIfNullOrWhiteSpace`. PASS.

---

## Summary

The TASK-045 streaming foundation is well-structured from a security perspective. No credentials, API keys, or secret fields are present in any of the new files. The keyed DI pattern correctly isolates per-exchange resources. Two non-blocking concerns are raised: (1) the multi-frame receive loop in `ClientWebSocketConnection.ReceiveAsync` has no maximum message size guard, which could allow a malicious server to drive unbounded memory growth — this is a hardening concern rather than a blocking flaw at this confidence level given that only trusted exchange endpoints are expected to connect; (2) `ConnectAsync` does not validate the URI scheme before delegating to `ClientWebSocket`, which could produce unclear error messages for misconfigured endpoints. Both are CONCERN-level (confidence < 80) and do not block approval.
