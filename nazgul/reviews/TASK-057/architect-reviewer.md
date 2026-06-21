---
verdict: CHANGES_REQUESTED
---
# Architect Review — TASK-057

## Verdict
CHANGES_REQUESTED

## Summary
The KuCoin signing infrastructure is functionally correct and the mark-and-strip DelegatingHandler pattern is faithfully replicated from OKX. However, `KucoinSigningHandler` accepts `KucoinSignatureService` (the concrete class) rather than `ISignatureService` (the interface), breaking the DIP mandate (Architectural Rule #11) that all other exchange signing handlers follow. Because `SignPassphrase()` does not exist on `ISignatureService`, the fix is to introduce a narrow `IKucoinSignatureService : ISignatureService` interface that adds `SignPassphrase(string)`.

---

## Findings

### Finding: KucoinSigningHandler takes concrete KucoinSignatureService instead of an interface
- **Severity**: MEDIUM
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Kucoin/Resilience/KucoinSigningHandler.cs:21`
- **Category**: Architecture (DIP — Invariant #11)
- **Verdict**: REJECT (blocking — confidence 95, severity MEDIUM)
- **Issue**: The handler constructor declares `KucoinSignatureService signatureService` (concrete). Every other exchange signing handler (OKX, Bybit, Bitget, Binance) uses `ISignatureService signatureService` (interface). The concrete binding bakes the implementation into the handler, makes the handler untestable via a mock implementing `SignPassphrase`, and violates the maintainer's 2026-06-18 DIP mandate: "Any type representing behavior the maintainer might swap … must be an interface resolved via DI, not a static class / concrete type that bakes the implementation in."
- **Fix**: Introduce `internal interface IKucoinSignatureService : ISignatureService` in `Auth/` with the additional `string SignPassphrase(string passphrase)` method. Have `KucoinSignatureService` implement that interface. Change the `KucoinSigningHandler` constructor parameter from `KucoinSignatureService` to `IKucoinSignatureService`. Update the test `BuildHandler` helper accordingly.
- **Pattern reference**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs:19` — `ISignatureService signatureService`; `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:19` — same; `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningHandler.cs:19` — same.

---

## Layering Check

**CLEAN — K1 invariant is not violated.**

- `CryptoExchanges.Net.Core` has no reference to KuCoin (only the pre-existing `ExchangeId.Kucoin` enum value, which is a scalar — not a type dependency).
- `CryptoExchanges.Net.Http` has no reference to KuCoin (one code comment in `StreamConnectionInfo.cs` is the only occurrence, not a using/reference).
- `KucoinSignatureService` imports only `CryptoExchanges.Net.Core.Auth`.
- `KucoinSigningHandler` imports `CryptoExchanges.Net.Core.Auth` and `CryptoExchanges.Net.Kucoin.Auth` — both correct.
- `KucoinErrorTranslator` imports `CryptoExchanges.Net.Core.Exceptions` and `CryptoExchanges.Net.Http` (for `RetryAfterReader`) — both correct within the Core → Http → Exchange chain.
- `KucoinSigningRequest` has no external imports — correct.
- The `CryptoExchanges.Net.Kucoin.csproj` `<ProjectReference>` nodes are `Core` and `Http` only — correct.

---

## Pattern Parity

| Aspect | OKX (reference) | KuCoin (this diff) | Match? |
|---|---|---|---|
| Handler base class | `DelegatingHandler` | `DelegatingHandler` | YES |
| `internal sealed` | Yes | Yes | YES |
| `SendAsync` override | `protected override async Task<HttpResponseMessage>` | Identical | YES |
| Unsigned pass-through | `if (!IsSigned) → base.SendAsync immediately` | Identical | YES |
| Per-attempt timestamp | Fresh `DateTimeOffset.UtcNow.AddMilliseconds(timeOffset())` in `ResignAsync` | Identical | YES |
| Strip-then-add headers | Remove all auth headers before re-adding | Strips all 5 KC-API-* headers before re-adding | YES |
| Request options marker | `HttpRequestOptionsKey<bool>("okx.signed")` | `HttpRequestOptionsKey<bool>("kucoin.signed")` | YES |
| Marker class name | `OkxSigningRequest` (internal static) | `KucoinSigningRequest` (internal static) | YES |
| File layout | `Auth/` for service, `Resilience/` for handler/marker/translator | Same | YES |
| Signature service type in handler | `ISignatureService` (interface) | `KucoinSignatureService` (concrete) | **DEVIATION** |
| POST body reading | `ReadAsStringAsync` on POST/PUT | Identical | YES |
| `ConfigureAwait(false)` | Present throughout | Present throughout | YES |
| Passphrase handling | Raw passphrase placed on header directly | HMAC-signed via `SignPassphrase` (KuCoin-specific passphrase-v2) | Justified exchange difference |

**One deviation identified**: the signature service constructor parameter is the concrete class, not an interface. All other exchanges pass `ISignatureService`. This is the blocking finding above.

**Timestamp format deviation is correct**: KuCoin uses Unix epoch milliseconds (`FormatTimestamp` returns numeric string), not ISO-8601 as OKX does. This is a genuine KuCoin API requirement and is correctly implemented.

**Five vs. four headers**: KuCoin requires an extra `KC-API-KEY-VERSION: 2` header that OKX does not have. The implementation strips and re-adds all five headers in the retry path — correct.

---

## Build & Test
- `dotnet build` (Release): **0 warnings, 0 errors**
- `dotnet test`: **44/44 tests pass** (KuCoinSigningTests: 40 new tests; ScaffoldSmokeTests: 5 existing)
