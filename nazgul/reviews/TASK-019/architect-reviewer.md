# Architect Review — TASK-019
**Verdict**: APPROVED
**Confidence**: 96

## Findings

### Finding: Static coupling to BitgetSignatureService.FormatTimestamp and BuildPrehash
- **Severity**: LOW
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningHandler.cs:54,71`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence 70 < 80)
- **Issue**: `BitgetSigningHandler.ResignAsync` calls two static methods on `BitgetSignatureService` directly: `FormatTimestamp(instant)` at line 54 and `BuildPrehash(...)` at line 71. The handler's ctor correctly depends on `ISignatureService` (Core interface) for the signing operation itself, but the timestamp formatting and prehash construction are coupled statically to the concrete `BitgetSignatureService`. If the timestamp format or prehash algorithm ever needs to change for a different Bitget API version, the handler is not swappable without modifying it. This is identical to the established OKX pattern (`OkxSigningHandler:48` calls `OkxSignatureService.FormatTimestamp`, `:63` calls `OkxSignatureService.BuildPrehash`), so it is not a defect introduced by this task — it is a cloned pattern. Confidence is kept below 80 because the pattern is deliberate and consistent across the exchange family; raising it as a concern so it surfaces if more exchanges compound this coupling.
- **Fix**: If the pattern grows to N=4+ exchanges and diverges, consider extending `ISignatureService` with a `FormatTimestamp` method, or extracting a separate `ISigningContextBuilder` interface. For this task alone, no change required.
- **Pattern reference**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs:48,63`

### Finding: Content-Type set on existing Content object inside the signing handler
- **Severity**: LOW
- **Confidence**: 65
- **File**: `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningHandler.cs:68`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence 65 < 80)
- **Issue**: Line 68 overwrites `request.Content.Headers.ContentType` inside the signing handler on every POST/PUT with content. This is a minor Single-Responsibility concern — setting `Content-Type` is a content construction concern that belongs in `BitgetHttpClient` when the `StringContent` is created, not in the retry-layer signing handler. The OKX handler (`OkxSigningHandler.cs:56-61`) does NOT set Content-Type at all, relying on `StringContent` to carry the correct header from construction. If `BitgetHttpClient` already creates `StringContent("...", Encoding.UTF8, "application/json")`, this line is redundant on the initial attempt and on retries. If it does not, the handler is patching a gap that belongs upstream. Either way, the signing handler is doing more than signing.
- **Fix**: Verify `BitgetHttpClient` sets `Content-Type: application/json` when constructing `StringContent`. If it does, remove line 68 from the handler. If it does not, fix it in the client — not in the signing handler. This is non-blocking because the result on the wire is correct regardless, and Bitget will receive the right header.
- **Pattern reference**: `src/CryptoExchanges.Net.Okx/Resilience/OkxSigningHandler.cs:56-61` (no Content-Type set in handler)

### Finding: Layering — no violations
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningHandler.cs:1-10`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. `using` directives reference only `CryptoExchanges.Net.Bitget.Auth` (same exchange assembly, same layer) and `CryptoExchanges.Net.Core.Auth` (Core). No Http, DI, or cross-exchange references.

### Finding: `internal sealed` visibility — correct
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningHandler.cs:24`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. `BitgetSigningHandler` is `internal sealed`, consistent with `OkxSigningHandler` and the invariant that exchange client internals stay internal. No public surface added.

### Finding: ISignatureService in ctor (not the concrete BitgetSignatureService)
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningHandler.cs:25`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. Ctor takes `ISignatureService` from Core. The handler is decoupled from `BitgetSignatureService` for the signing operation, consistent with the REF-002 OKX seam and invariant 11 (interfaces over static for swappable behavior). The concrete implementation is injected at composition time.

### Finding: Re-sign on retry — correct strip-then-add pattern
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningHandler.cs:74-83`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. All four `ACCESS-*` headers are stripped before re-adding on every `ResignAsync` call, ensuring exactly one set of headers after any number of retries. Pattern matches OKX.

### Finding: Unsigned request pass-through
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningHandler.cs:34-37`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. `BitgetSigningRequest.IsSigned(request)` gates all auth header logic. Public endpoint requests pass through without any `ACCESS-*` headers added, matching the task acceptance criterion 3 and OKX pattern.

### Finding: Path/query SPLIT for BuildPrehash — Bitget-specific delta, correctly handled
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningHandler.cs:59-60`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. Bitget's `BuildPrehash` requires path and query separately (it re-inserts `?` only when query is non-empty). The handler correctly splits `RequestUri.AbsolutePath` and `RequestUri.Query.TrimStart('?')` and passes them independently, rather than using `PathAndQuery` as OKX does. This is the documented Bitget-specific delta in the task notes, and is byte-for-byte consistent with how `BitgetHttpClient` would construct the URI.

### Finding: Fail-fast guards on apiKey and passphrase
- **Severity**: LOW
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningHandler.cs:45-51`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. `InvalidOperationException` with descriptive messages when `apiKey` or `passphrase` is null/empty on a signed request, matching OKX's pattern and acceptance criterion 3.

### Finding: XML doc comment quality and LEAN inline comments
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bitget/Resilience/BitgetSigningHandler.cs:13-25`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. Type-level XML doc on the handler and all ctor params. Inline comments are purposeful (explain WHY for the path/query split, the strip-before-add, the guard rationale). Comment density matches OKX reference. No superfluous narration.

## Summary
`BitgetSigningHandler` is a faithful and correctly architected implementation of the established header-based signing handler pattern, mirroring `OkxSigningHandler` with the documented Bitget-specific delta (path+query split for `BuildPrehash`). Layering is clean — only Core and same-exchange-assembly references. The handler is `internal sealed`, depends on `ISignatureService` (not the concrete), re-signs on retry, and guards all three Bitget credentials correctly. Two non-blocking CONCERNs are noted: the static coupling to `BitgetSignatureService.FormatTimestamp/BuildPrehash` is a cloned pattern that will compound if more exchanges follow without refactoring (flag for milestone-boundary review), and `Content-Type` is set inside the signing handler where it belongs on the content at construction time in `BitgetHttpClient`. Neither blocks this task.
