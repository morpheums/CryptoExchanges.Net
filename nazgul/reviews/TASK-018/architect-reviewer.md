# Architect Review — TASK-018

**VERDICT: APPROVED**
**Overall confidence: 97/100**
**Blocking items: 0**

---

## Scope

Review limited to:
- `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs`
- `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningRequest.cs`

Pattern references consulted:
- `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs`
- `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningRequest.cs`
- `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs`
- `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningRequest.cs`
- `src/CryptoExchanges.Net.Core/Auth/ISignatureService.cs`
- `src/CryptoExchanges.Net.Core/Auth/SignatureEncoding.cs` (contains `HmacSignature`)

---

## Findings

### Finding: ISignatureService seam conformance
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs:11,16-17`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- `internal sealed class BitgetSignatureService(string secretKey) : ISignatureService` — declaration matches OKX seam (`OkxSignatureService.cs:10`). `Sign(string payload)` carries `/// <inheritdoc />` and delegates to `HmacSignature.Compute(_secretKey, payload, SignatureEncoding.Base64)` — no reimplemented crypto, correct base64 encoding for Bitget. Consistent with OKX at `OkxSignatureService.cs:15-16`.

### Finding: Layering — no cross-layer or cross-exchange references
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs:1-2`; `src/CryptoExchanges.Net.Bitget/CryptoExchanges.Net.Bitget.csproj:12-14`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- Only `using System.Globalization` and `using CryptoExchanges.Net.Core.Auth`. Csproj `<ProjectReference>` nodes reference only Core and Http — no Binance, OKX, or DI. Dependency direction is correct.

### Finding: Internal visibility — no accidental public surface
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs:11`; `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningRequest.cs:5`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- `BitgetSignatureService` is `internal sealed`; `BitgetSigningRequest` is `internal static`. `BuildPrehash` and `FormatTimestamp` are `public static` on an `internal sealed` class — access is bounded by the class visibility so no surface leaks through the assembly boundary. OKX (`OkxSignatureService.cs:34`, `OkxSigningRequest.cs:9-20`) and Binance follow the identical pattern. No new types added to the public API of any shared interface.

### Finding: Prehash formula correctness (Bitget delta vs OKX)
- **Severity**: N/A
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs:25-35`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- Formula: `timestamp + UPPER(method) + requestPath + ('?' + queryString when non-empty) + body`. Correctly captures the Bitget-specific delta: OKX folds the query string into `requestPath`; Bitget receives it as a separate `queryString` parameter and conditionally appends `?queryString`. Guard clause split is correct per ADR-001 conv 4: `ThrowIfNullOrWhiteSpace` on identity params (timestamp, method, requestPath), `ThrowIfNull` on nullable-but-possibly-empty params (queryString, body). Line 33 conditional `queryString.Length > 0 ? $"?{queryString}" : string.Empty` is correct.

### Finding: Timestamp format (epoch-ms vs OKX ISO-8601)
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs:37-39`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- `ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture)` — correct epoch-millisecond string per Bitget API spec. Correctly differs from OKX's ISO-8601 format (`OkxSignatureService.cs:49-50`). `CultureInfo.InvariantCulture` prevents locale-dependent formatting of the integer.

### Finding: Secret guard pattern
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs:41-45`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- `InitializeSecretKey` uses `ThrowIfNullOrWhiteSpace` and returns the string. Matches `OkxSignatureService.cs:52-56` exactly. Note: Bitget stores the secret as `string` (like OKX), while Binance stores as `byte[]` (pre-existing divergence in Binance; not introduced here).

### Finding: BitgetSigningRequest marker
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningRequest.cs:1-22`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- `HttpRequestOptionsKey<bool>` keyed `"bitget.signed"` — exchange-scoped, no collision with `"okx.signed"` (`OkxSigningRequest.cs:7`) or `"binance.signed"` (`BinanceSigningRequest.cs:7`). `MarkSigned`/`IsSigned` with `ThrowIfNull` guards are idempotent by definition (`Options.Set` overwrites; `TryGetValue` is a read). Mirrors `OkxSigningRequest.cs` line-for-line except for the namespace and key string.

### Finding: Lean comments (ADR-001 conv 7)
- **Severity**: N/A
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs:6-16,19-30,37,43-44`; `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningRequest.cs:3-4,9,14`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- `Sign` carries `<inheritdoc />` — correct. `BuildPrehash` has a single concise `<summary>` that states the formula and the Bitget-vs-OKX delta (non-obvious "why", not a restatement of code). Notably leaner than OKX's `BuildPrehash` which has verbose `<param>/<returns>/<exception>` tags (`OkxSignatureService.cs:21-33`) — the Bitget approach is more compliant with ADR-001 conv 7 ("add `<param>`/`<returns>`/`<exception>` only when they add information the signature doesn't"). `FormatTimestamp` has a single-line summary. `InitializeSecretKey` (private) has no comment — correct. No over-commented lines.

### Finding: No static mutable state
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Auth/BitgetSignatureService.cs`; `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningRequest.cs`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None.
- `_secretKey` is `readonly`; `SignedKey` in `BitgetSigningRequest` is `static readonly`. No mutable static fields.

---

## Summary

- PASS: ISignatureService seam — `internal sealed`, `<inheritdoc />` on `Sign`, delegates to `HmacSignature.Compute` with `SignatureEncoding.Base64`. Exact OKX pattern.
- PASS: Layering — Core.Auth only; csproj references Core + Http only.
- PASS: Internal visibility — no accidental public surface; `public static` helpers on `internal sealed` class are bounded by class visibility, consistent with OKX and Binance.
- PASS: Prehash formula — correctly captures Bitget delta (`queryString` as separate param, conditionally prepended with `?`). Guard clauses split correctly per ADR-001 conv 4.
- PASS: Timestamp format — epoch-ms string with `InvariantCulture`. Correct Bitget-specific behavior distinct from OKX ISO-8601.
- PASS: Secret guard — `InitializeSecretKey` with `ThrowIfNullOrWhiteSpace`. Mirrors OKX exactly.
- PASS: BitgetSigningRequest marker — `"bitget.signed"` key, idempotent, mirrors OkxSigningRequest line-for-line.
- PASS: Lean comments (ADR-001 conv 7) — `<inheritdoc />` on `Sign`; concise `<summary>` on `BuildPrehash` stating the non-obvious Bitget delta; no over-commenting.
- PASS: No static mutable state.

---

## Final Verdict

**APPROVED** — confidence 97/100 — 0 blocking items.

All architectural invariants satisfied. Both files conform to the post-REF-002 ISignatureService seam, correct layering, internal visibility rules, lean-comment mandate (ADR-001 conv 7), and the established signing-marker pattern. No cross-exchange or cross-layer pollution. No new public API surface on shared interfaces. Implementation is ready to proceed to test coverage (TASK-022).
