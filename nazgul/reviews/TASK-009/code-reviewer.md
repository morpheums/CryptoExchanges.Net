# Code Review — TASK-009: OKX-era credential/signing generalization (Core/Auth)

**Reviewer**: Code Reviewer
**Commit**: 63b0006
**Branch**: feat/m3-okx
**Date**: 2026-06-18

---

## Files Reviewed

- `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs`
- `src/CryptoExchanges.Net.Core/Auth/SignatureEncoding.cs`
- `tests/CryptoExchanges.Net.Core.Tests.Unit/AuthTests.cs`
- `nazgul/tasks/TASK-009.md`

---

## Findings

### Finding 1: Passphrase guard logic is correct
- **Severity**: N/A (PASS)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:44-45`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: The passphrase guard is `if (passphrase is not null) ArgumentException.ThrowIfNullOrWhiteSpace(passphrase)`. This is the correct pattern: `null` passes (valid absent state for Binance/Bybit), and a non-null empty or whitespace string throws. The pattern exactly matches the spec in the task manifest.

---

### Finding 2: Undefined-enum switch path
- **Severity**: N/A (PASS)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Core/Auth/SignatureEncoding.cs:47-52`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: The switch expression includes `_ => throw new ArgumentOutOfRangeException(nameof(encoding), encoding, "Unknown signature encoding.")`. The discard arm fires for any undefined integer cast like `(SignatureEncoding)999`. The test `Compute_Throws_OnUndefinedEncoding` exercises this path with `(SignatureEncoding)999`.

---

### Finding 3: Mask helper — range operator and off-by-one
- **Severity**: N/A (PASS)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:63-64`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: `value.Length <= 4 ? "****" : $"****{value[^4..]}"`. For a string of length 2 (`"ab"`), the guard branches left before `[^4..]` is ever evaluated, so no `ArgumentOutOfRangeException`. For exactly 4 chars (`"abcd"`), the `<= 4` guard still takes the left branch. For 5+ chars, `[^4..]` yields the last 4 characters correctly. No off-by-one.

---

### Finding 4: Missing exactly-4-char ApiKey boundary test
- **Severity**: LOW
- **Confidence**: 85
- **File**: `tests/CryptoExchanges.Net.Core.Tests.Unit/AuthTests.cs:143-146`
- **Category**: Testing
- **Verdict**: CONCERN (non-blocking, confidence 85/100)
- **Issue**: `ToString_MasksShortApiKeyEntirely` uses `"ab"` (2 chars). The test for masking a long key uses `"publicApiKey1234"` (16 chars). Neither test covers ApiKey of exactly 4 characters — the boundary case where `value.Length <= 4` evaluates to `true` for exactly 4. The behavior is correct (returns `"****"`), but the boundary is untested.
- **Fix**: Add `new ExchangeCredentials("abcd", "secret").ToString().Should().Contain("ApiKey = ****").And.NotContain("abcd")` as a new test case to pin the exactly-4-char boundary.
- **Pattern reference**: `ExchangeCredentials.cs:63-64`

---

### Finding 5: Build cleanliness — 0 warnings, 0 errors
- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: Solution-wide
- **Category**: Roslyn analyzer compliance
- **Verdict**: PASS
- **Issue**: `dotnet build CryptoExchanges.Net.sln --configuration Release` reported `0 Warning(s), 0 Error(s)` under `TreatWarningsAsErrors=true`, `AnalysisLevel=latest-all`, `Nullable=enable`, `GenerateDocumentationFile=true`. No new `#pragma warning disable` or `NoWarn` entries were added.

---

### Finding 6: Non-breaking Binance/Bybit confirmation
- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: Commit `63b0006` diff
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: The commit touches exactly 4 files: `TASK-009.md` (manifest), `ExchangeCredentials.cs` (new), `SignatureEncoding.cs` (new), `AuthTests.cs` (new). Zero Binance, Bybit, Http, or DI source files were modified. Bybit (80 tests) and Binance integration (45 tests) all pass unchanged.

---

### Finding 7: Hex/base64 vectors independently verified
- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: `tests/CryptoExchanges.Net.Core.Tests.Unit/AuthTests.cs:19-21`
- **Category**: Testing
- **Verdict**: PASS
- **Issue**: The pinned hex `88aab3ede8d3adf94d26ab90d3bafd4a2083070c3bcce9c014ee04a443847c0b` and base64 `iKqz7ejTrflNJquQ07r9SiCDBww7zOnAFO4EpEOEfAs=` were independently re-derived during this review via Python `hmac`/`hashlib`/`base64` — both match exactly. The test `Compute_HexAndBase64_AreSameUnderlyingHash` additionally proves both encodings decode to the same byte sequence, making the assertion non-tautological.

---

### Finding 8: Guards — ThrowIfNullOrWhiteSpace on all required inputs
- **Severity**: N/A (PASS)
- **Confidence**: 99
- **File**: `ExchangeCredentials.cs:42-43`, `SignatureEncoding.cs:40-41`
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: `apiKey`, `secretKey`, `secret`, and `payload` all have `ArgumentException.ThrowIfNullOrWhiteSpace`. The blank-input theories in tests cover `null`, `""`, and `"   "` for each guard site.
- **Pattern reference**: `src/CryptoExchanges.Net.Core/SymbolMapper.cs:27`

---

### Finding 9: .NET 10 idioms — Convert.ToHexStringLower, HMACSHA256.HashData
- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: `SignatureEncoding.cs:45,49-50`
- **Category**: Style
- **Verdict**: PASS
- **Issue**: `HMACSHA256.HashData(secretBytes, payloadBytes)` and `Convert.ToHexStringLower(hash)` / `Convert.ToBase64String(hash)` are idiomatic .NET 10 primitives, consistent with `BinanceSignatureService.cs:21-22` and `BybitSignatureService.cs:25-26`.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:21-22`

---

### Finding 10: XML documentation coverage
- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: `ExchangeCredentials.cs:3-12`, `SignatureEncoding.cs:6-17`, `SignatureEncoding.cs:19-37`
- **Category**: Code Quality
- **Verdict**: PASS
- **Issue**: Every public type, property, constructor, and method has `<summary>` docs. The `Compute` method has `<param>`, `<returns>`, and `<exception>` tags for all three exception cases. Enum members have `<summary>` with `<see cref>` to the backing API. The remarks block on `ExchangeCredentials` references ADR-001.

---

### Finding 11: Thread safety — no mutable shared state
- **Severity**: N/A (PASS)
- **Confidence**: 100
- **File**: Both source files
- **Category**: Correctness
- **Verdict**: PASS
- **Issue**: `ExchangeCredentials` is an immutable `sealed record` with `get`-only properties. `HmacSignature` is a `static` class with no mutable fields. No thread-safety concerns apply.

---

### Finding 12: Record synthesized PrintMembers — sealed prevents secret leak
- **Severity**: LOW
- **Confidence**: 70
- **File**: `ExchangeCredentials.cs:13,59`
- **Category**: Correctness
- **Verdict**: CONCERN (non-blocking, confidence 70/100)
- **Issue**: In C# records, overriding `ToString()` does NOT eliminate the compiler-synthesized `protected virtual bool PrintMembers(StringBuilder)`. However, since the record is `sealed`, `PrintMembers` is never invoked externally, and the `public override string ToString()` never delegates to `base.ToString()` or `PrintMembers`. Risk is effectively zero.
- **Pattern reference**: `ExchangeCredentials.cs:59` — `override string ToString()` does not call `base.ToString()`.

---

## Build and Test Results

- `dotnet build CryptoExchanges.Net.sln --configuration Release` — **Build succeeded. 0 Warning(s), 0 Error(s).**
- Core unit tests: **92 passed** (was 68; +24 new auth tests)
- Http unit tests: **12 passed**
- DI unit tests: **10 passed**
- Bybit unit tests: **80 passed** (non-breaking confirmed)
- Binance integration tests: **45 passed** (non-breaking confirmed)

---

## Summary

- PASS: Guards — `ThrowIfNullOrWhiteSpace` on all required inputs; passphrase guard is null-tolerant and whitespace-rejecting as specified.
- PASS: Undefined-enum handling — discard arm throws `ArgumentOutOfRangeException` with `nameof(encoding)` and offending value; tested.
- PASS: Mask helper — `value.Length <= 4` guard prevents `[^4..]` on short strings; no off-by-one.
- PASS: Build — 0 warnings, 0 errors under `TreatWarningsAsErrors=true`; all 92 Core unit tests pass.
- PASS: Hex/base64 vectors — independently verified; pinned constants correct; same-underlying-hash assertion is non-tautological.
- PASS: XML docs — complete on every public type, property, constructor, and method.
- PASS: Non-breaking — zero Binance/Bybit/Http/DI source files modified; all downstream tests pass.
- PASS: Idioms — `HMACSHA256.HashData`, `Convert.ToHexStringLower`, `Convert.ToBase64String` are .NET 10 idiomatic.
- CONCERN: Missing exactly-4-char ApiKey boundary test (confidence: 85/100, non-blocking) — behavior is correct but boundary untested.

---

## Final Verdict

**APPROVED**

The implementation is correct, idiomatic, and complete. Build is clean under `TreatWarningsAsErrors=true`. All guards match the project convention. The passphrase null-vs-whitespace logic is correctly specified and tested. HMAC vectors are independently verified. The only finding is a minor missing boundary test (exactly-4-char ApiKey) that does not affect correctness and is non-blocking.
