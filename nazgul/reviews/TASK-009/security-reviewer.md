# Security Review — TASK-009: OKX-era credential/signing generalization (Core)

**Reviewer**: Security Reviewer
**Branch**: feat/m3-okx
**Commit**: 63b0006
**Date**: 2026-06-18
**Files reviewed**:
- `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs`
- `src/CryptoExchanges.Net.Core/Auth/SignatureEncoding.cs`
- `tests/CryptoExchanges.Net.Core.Tests.Unit/AuthTests.cs`
- `nazgul/tasks/TASK-009.md`

---

## Findings

### Finding 1: HMAC primitive correctness
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Core/Auth/SignatureEncoding.cs:43-45`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None.
- **Analysis**: `HMACSHA256.HashData(secretBytes, payloadBytes)` is the BCL static method. Argument order is `(key, source)` — `secretBytes` is the key, `payloadBytes` is the message. Correct. Both are UTF-8 encoded via `Encoding.UTF8.GetBytes`. No homemade crypto. Identical primitive to `BinanceSignatureService.Sign` at `BinanceSignatureService.cs:20-22`. The test at `AuthTests.cs:28-33` independently re-derives the same hash inline and asserts equality, making byte-identity provable. Hex vector `88aab3ede8d3adf94d26ab90d3bafd4a2083070c3bcce9c014ee04a443847c0b` matches the Binance/Bybit pinned vector.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs:20-22`

---

### Finding 2: Secret leakage via ToString — sealed record PrintMembers analysis
- **Severity**: N/A
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:59-61`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None.
- **Analysis**: Precise C# specification behavior for `sealed record`:

  1. The C# compiler synthesizes `public override string ToString()` on a record, which internally calls `PrintMembers(StringBuilder)`.
  2. The synthesized `protected virtual bool PrintMembers(StringBuilder builder)` would append all properties including `SecretKey` and `Passphrase`.
  3. When `ExchangeCredentials` provides its own `public override string ToString()`, the compiler sees an explicit override and does NOT synthesize its own `ToString()`. The override fully replaces the synthesized one. The synthesized version is never generated for this type.
  4. `PrintMembers` is still synthesized (C# 10+ always synthesizes it for records), but it is `protected virtual`. Its only callers are: (a) the synthesized `ToString()` — suppressed by the override; (b) a derived type's `PrintMembers` calling `base.PrintMembers()`. The `sealed` modifier prevents any derived types, making (b) impossible.
  5. An outer record containing an `ExchangeCredentials` property would call `.ToString()` on it (compiler generates `builder.Append(PropertyName.ToString())`), not `PrintMembers()` directly. So an outer record invokes the overridden, redacted `ToString()`.

  Conclusion: the override at `ExchangeCredentials.cs:59-61` fully suppresses the synthesized printer. `SecretKey` and `Passphrase` values cannot reach any string rendering path.

---

### Finding 3: ApiKey masking — edge case review
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:63-64`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None.
- **Analysis**: `Mask` uses `value[^4..]` (range operator). For `value.Length > 4`, returns last 4 characters; for `value.Length <= 4`, returns `"****"` entirely. The constructor guard `ArgumentException.ThrowIfNullOrWhiteSpace(apiKey)` ensures the string is never null or empty before `Mask` is called — no null-dereference or index-out-of-range risk. Revealing 4 characters of a typically 20-64 character API key is standard industry practice and does not materially help an attacker. Tests at `AuthTests.cs:133-146` cover both the long-key (last-4 visible) and short-key (fully masked) cases.

---

### Finding 4: Input guards completeness
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:42-45`, `SignatureEncoding.cs:40-41`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None.
- **Analysis**:
  - `ExchangeCredentials`: `ThrowIfNullOrWhiteSpace(apiKey)`, `ThrowIfNullOrWhiteSpace(secretKey)`, and for non-null passphrase: `ThrowIfNullOrWhiteSpace(passphrase)`. Null passphrase is the valid absent state and is explicitly allowed. Guards are complete.
  - `HmacSignature.Compute`: `ThrowIfNullOrWhiteSpace(secret)`, `ThrowIfNullOrWhiteSpace(payload)`, and undefined enum throws `ArgumentOutOfRangeException`. Guards are complete.
  - Test theory at `AuthTests.cs:99-119` covers null, empty, and whitespace-only for all three constructor parameters.

---

### Finding 5: Timing/constant-time concerns
- **Severity**: LOW
- **Confidence**: 30
- **File**: `src/CryptoExchanges.Net.Core/Auth/SignatureEncoding.cs:38-53`
- **Category**: Security
- **Verdict**: PASS (noted, non-applicable)
- **Analysis**: This is a signing path, not a comparison path. HMAC output is transmitted to the exchange for server-side verification. There is no local secret-comparison being performed that could be vulnerable to timing attacks. Non-blocking by design.

---

### Finding 6: Serialization exposure
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None.
- **Analysis**: No `[JsonInclude]`, `[JsonPropertyName]`, `[Serializable]`, or any serialization attribute is present on `ExchangeCredentials`. `System.Text.Json` does not serialize get-only constructor-assigned properties without explicit opt-in attributes. No serialization exposure path exists.

---

### Finding 7: Test coverage of redaction robustness
- **Severity**: N/A
- **Confidence**: 95
- **File**: `tests/CryptoExchanges.Net.Core.Tests.Unit/AuthTests.cs:122-146`
- **Category**: Security
- **Verdict**: PASS
- **Issue**: None.
- **Analysis**: `ToString_DoesNotLeakSecretOrPassphrase` at line 122 explicitly asserts `NotContain("topSecretValue")` and `NotContain("myPassphrase")` on the result of `.ToString()`. These assertions use the actual secret and passphrase strings as the search terms, making the test a genuine absence check. `ToString_MasksApiKey_RevealingOnlyLastFour` at line 133 implicitly covers secret redaction for the no-passphrase case. Coverage is sufficient.

---

## Summary

- PASS: HMAC primitive — `HMACSHA256.HashData(key, message)` with correct UTF-8 encoding, BCL only, byte-identical to Binance. Confidence: 100/100.
- PASS: ToString/PrintMembers sealed-record interaction — overriding `ToString()` on a `sealed record` fully suppresses the synthesized printer. `PrintMembers` has no reachable callers. `SecretKey` and `Passphrase` cannot be rendered. Confidence: 99/100.
- PASS: ApiKey masking — last-4 reveal is industry-standard, short-key fully masked, no null/range risk. Confidence: 100/100.
- PASS: Input guards — complete coverage of all constructor and method parameters. Confidence: 100/100.
- PASS: Serialization — no serialization attributes, no JSON exposure path. Confidence: 100/100.
- PASS: Test coverage of redaction — explicit substring-absence assertions on the actual secret values. Confidence: 95/100.

---

## Final Verdict

**APPROVED** — Confidence: 99/100

All security checks pass. The `ToString` override on the `sealed record` correctly and fully suppresses the synthesized `PrintMembers` printer; no path exists by which `SecretKey` or `Passphrase` values can be rendered. The HMAC primitive is the BCL `HMACSHA256.HashData` with correct key/message argument order and UTF-8 encoding, byte-identical to the Binance implementation. Input guards are complete. No serialization exposure. No blocking findings.
