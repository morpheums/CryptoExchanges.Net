# Architect Review — TASK-011

**Reviewer**: Architect  
**Date**: 2026-06-18  
**Task**: TASK-011 — OkxSignatureService (base64 prehash) + OkxSigningRequest marker  
**Files in scope** (strict):
- `src/CryptoExchanges.Net.Okx/Auth/OkxSignatureService.cs`
- `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningRequest.cs`

---

## Checklist Walk-through

### Dependency direction (Core → Http → Exchange)
- `OkxSignatureService.cs` imports `CryptoExchanges.Net.Core.Auth` (HmacSignature + SignatureEncoding). Exchange → Core is the correct direction. No Http, DI, or Binance namespace appears.
- `OkxSigningRequest.cs` has no `using` statements beyond implicit globals. `GlobalUsings.cs` references only Core (Enums, Interfaces, Models) and BCL. Clean.
- `CryptoExchanges.Net.Okx.csproj` has `<ProjectReference>` to `Core` and `Http` only. No reference to Binance, Bybit, or DI assemblies. Correct.

### Visibility / internal surface (ADR-001 conv #2)
- `OkxSignatureService` — `internal sealed class`. Correct.
- `OkxSigningRequest` — `internal static class`. Correct.
- This is an improvement over the Bybit reference pattern: `BybitSigningRequest` was accidentally left `public` (confirmed at `BybitSigningRequest.cs:5`). OKX correctly uses `internal`. The deviation is intentional per the task implementation notes ("mirrors BybitSigningRequest exactly — internal static class") and aligns with the canonical Binance pattern (`BinanceSigningRequest.cs:5` — `internal static class`) and ADR-001 conv #2.

### No re-implemented crypto
No `HMACSHA256`, `Convert.ToBase64String`, or `Encoding.UTF8` appears in either OKX file. `Sign(string prehash)` delegates entirely to `HmacSignature.Compute(_secretKey, prehash, SignatureEncoding.Base64)`. Correct: the Core primitive owns the hash + encoding (TASK-009). OKX secret is stored as `string` (not `byte[]`) because `HmacSignature.Compute` handles UTF-8 encoding internally — correct and intentional.

### Prehash contract matches OKX spec
`BuildPrehash` assembles `{timestamp}{method.ToUpperInvariant()}{requestPath}{body}`. Per OKX V5 API: `timestamp + method + requestPath + body`. The method is ToUpperInvariant() per the doc note in the source. GET query strings are expected in `requestPath` by convention (documented); POST body is a raw JSON string (empty for GET/DELETE). Correct.

### ISO-8601 timestamp format
`FormatTimestamp` uses `"yyyy-MM-ddTHH:mm:ss.fffZ"` with `CultureInfo.InvariantCulture`. This produces e.g. `2026-06-17T12:00:00.000Z` — millisecond precision with a trailing literal `Z`. Note: using the format specifier `Z` (literal, not timezone format character) combined with explicit `.ToUniversalTime()` is intentional and correct — `"Z"` in a custom format string is a literal character, not the timezone offset designator `%z`. The result matches the OKX API requirement. Correct.

### Guard clauses
- `timestamp`, `method`, `requestPath` → `ThrowIfNullOrWhiteSpace`. Correct per ADR-001 conv #4 (whitespace-free identity values).
- `body` → `ThrowIfNull` only (empty body is valid for GET; disallowing whitespace would reject `""` which is the legitimate no-body form). Correct asymmetry.
- `secretKey` guard in `InitializeSecretKey` matches Binance/Bybit secret-key pattern exactly.
- `MarkSigned` / `IsSigned` guard `request` with `ThrowIfNull`. Correct.

### Static testability helpers
Both `BuildPrehash` and `FormatTimestamp` are `static` — unit tests in TASK-015 can assert them without constructing the service. Correct and intentional.

### OkxSigningRequest key namespace isolation
Key string is `"okx.signed"`. Bybit uses `"bybit.signed"`, Binance uses `"binance.signed"`. No collision. `MarkSigned` is idempotent (repeated `Set` overwrites with the same `true`). `IsSigned` reads cleanly. Correct.

### InternalsVisibleTo
`CryptoExchanges.Net.Okx.csproj` exposes internals to `CryptoExchanges.Net.Okx.Tests.Unit`, `CryptoExchanges.Net.Okx.Tests.Integration`, and `DynamicProxyGenAssembly2`. No DI package referenced yet (no `AddOkxExchange` written yet). Correct for this task scope; the DI package IVT will be added in the DI registration task.

### Build verification
`dotnet build CryptoExchanges.Net.sln --no-incremental` → **Build succeeded. 0 Warning(s). 0 Error(s).** TreatWarningsAsErrors is satisfied.

---

## Findings

### Finding 1: BybitSigningRequest is public — pre-existing pattern divergence from ADR-001 conv #2

- **Severity**: LOW
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs:5`
- **Category**: Architecture (pre-existing, not introduced by TASK-011)
- **Verdict**: CONCERN (non-blocking — this is a pre-existing issue not introduced in this diff; OKX correctly uses `internal`)
- **Issue**: `BybitSigningRequest` is `public static class` while ADR-001 conv #2 requires all signing, services, HTTP wrappers, and handlers to be `internal`. Binance's `BinanceSigningRequest` is correctly `internal`. OKX correctly uses `internal`. Bybit's public visibility leaks an implementation type into the assembly's public surface and lets external callers flag arbitrary requests as Bybit-signed without going through the DI-assembled handler chain.
- **Fix**: Change `BybitSigningRequest` to `internal static class`. Add `InternalsVisibleTo("CryptoExchanges.Net.Bybit.Tests.Unit")` if tests call it directly (check test project for references). This is a binary-compatible change (no external consumer should be calling this type).
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningRequest.cs:5` — `internal static class BinanceSigningRequest`; `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningRequest.cs:5` — `internal static class OkxSigningRequest`

---

## Summary

- PASS: `OkxSignatureService` — `internal sealed`, correct dependency direction (Core only), no re-implemented crypto, delegates to `HmacSignature.Compute(..., SignatureEncoding.Base64)`, static helpers testable independently.
- PASS: `OkxSigningRequest` — `internal static`, correct `"okx.signed"` key (no collision), idempotent `MarkSigned`, guards on all public members, mirrors `BinanceSigningRequest` (the canonical pattern, not Bybit's accidentally-public form).
- PASS: Dependency graph — OKX project references Core + Http only; no Binance/Bybit/DI coupling.
- PASS: Build — clean, 0 warnings, 0 errors under `TreatWarningsAsErrors`.
- PASS: Prehash contract — `timestamp + METHOD.ToUpperInvariant() + requestPath + body` matches OKX V5 spec; ISO-8601 timestamp with ms + literal Z matches exchange requirement.
- CONCERN: `BybitSigningRequest` public visibility (pre-existing, not in this diff) — `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs:5` — violates ADR-001 conv #2; OKX correctly diverges from Bybit by using `internal`. Fix: change Bybit's marker to `internal` in a follow-up task. (confidence: 90/100, non-blocking)

---

## Final Verdict

**APPROVED**

No blocking findings. The two files fully satisfy TASK-011 acceptance criteria, follow ADR-001 conventions (both `internal`), delegate HMAC correctly to Core without re-implementation, and produce a clean build. The single concern is a pre-existing Bybit visibility issue not introduced by this diff — the OKX implementation is the correct form and can serve as the reference pattern when the Bybit type is harmonized.
