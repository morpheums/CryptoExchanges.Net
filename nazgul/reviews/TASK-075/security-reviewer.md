# Security Review — TASK-075 (Retry 1)

## Verdict: APPROVED

---

## F1 Fix Security Check

**Finding addressed:** Missing `turnover` field in `StreamKlineDto` (correctness issue, not a security issue).

**Fix applied at commit `4fe3ff2`:** `StreamKlineDto` now contains:

```csharp
/// <summary>Quote-asset volume (turnover).</summary>
[JsonPropertyName("turnover")]
public string Turnover { get; init; } = "0";
```

The fix introduces no security concern. `Turnover` is a plain `string` property with a safe default of `"0"`, positioned after `Volume` and before `Interval`. The type, default, attribute pattern, and placement are identical to the `Volume` field directly above it and to Binance's equivalent `QuoteVolume` field in `StreamKlineBarDto`. No credential, authentication material, or sensitive data is involved. No serialization path, logging, or `ToString()` override is added. No attack surface is expanded.

---

## Findings

No new blocking security findings.

The five files under review (`BybitStreamOptions.cs`, `StreamKlineDto.cs`, `StreamTickerDto.cs`, `StreamTradeDto.cs`, `StreamDepthDto.cs`) are unchanged from the first review in all respects relevant to security:

- All four wire DTOs remain `internal sealed record` with `init`-only properties. No `public` exposure of wire shapes.
- No credential fields (`ApiKey`, `SecretKey`, or equivalent) appear anywhere in the diff.
- No `[JsonInclude]`, `[JsonExtensionData]`, or `dynamic`/`object` deserialization targets are present.
- No `ToString()` override that could surface sensitive data.
- No environment variable reads, no hardcoded credentials, no new logging paths.
- `BybitStreamOptions.StreamBaseUrl` is unchanged: mutable `string`, default `wss://stream.bybit.com/v5/public/spot`, no properties removed or validation bypassed since the prior review.

---

## Concerns (non-blocking)

### C1 — `StreamBaseUrl` has no URI scheme validation
- **Severity**: LOW
- **Confidence**: 55/100 (below 80 — non-blocking, awareness only)
- **File**: `src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamOptions.cs:12`
- **Status**: Unchanged from prior review. This concern carries forward at the same low confidence level. The property setter does not validate that the caller supplies a `wss://` scheme. A misconfigured caller supplying `ws://` would silently downgrade to plaintext WebSocket. The default is correct. Enforcement belongs at connection time in TASK-078's `BybitStreamProtocol.ResolveConnectionAsync`, consistent with how the Binance streaming protocol consumes its options class.
- **No action required to approve this task.** TASK-078 is the appropriate enforcement point.
