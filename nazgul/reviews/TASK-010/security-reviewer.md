# Security Review: TASK-010 — OKX project scaffold + passphrase options + DI seam stub

**Reviewer**: Security Reviewer
**Date**: 2026-06-18
**Branch**: feat/m3-okx
**Commit**: af642795acfca20118a0f73c8a87c0de7c615b73
**Verdict**: APPROVED
**Confidence**: 92/100

---

## Scope

Pure structural scaffolding: three new source files (`OkxOptions.cs`, `GlobalUsings.cs`,
`CryptoExchanges.Net.Okx.csproj`) plus a `.sln` modification. No signing handler, HTTP client,
error translator, or DI registration is introduced in this task. Security surface is limited to
credential field declarations and the `ToCredentials()` factory.

---

## Checklist Evaluation

### Credential Safety

- No new code stores `SecretKey` or `Passphrase` in any field outside a signing handler or
  signature service. All three credential fields on `OkxOptions` are plain `{ get; set; }` string
  properties with empty-string defaults, consistent with the Bybit convention.
- No logging, serialization, or exception-message inclusion of `ApiKey`, `SecretKey`, or
  `Passphrase` appears anywhere in the diff.
- `OkxOptions` has no `[JsonInclude]`, `[JsonPropertyName]`, or any other JSON serialization
  attribute on any property. The class is not annotated for serialization.
- `SecretKey` and `Passphrase` are not transmitted — `ToCredentials()` hands them to
  `ExchangeCredentials`, which is the validated Core credential type; it never constructs an HTTP
  request.
- No secrets are hardcoded; all defaults are `string.Empty`.

### `ToCredentials()` Analysis

`OkxOptions.ToCredentials()` delegates directly to `new ExchangeCredentials(ApiKey, SecretKey,
Passphrase)`. `ExchangeCredentials` (Core) validates all three arguments with
`ArgumentException.ThrowIfNullOrWhiteSpace` and throws for empty/whitespace values. This is safe:
validation is centralised in Core and will reject empty credentials at call time.

### `ToString()` / Redaction Posture

`OkxOptions` does not override `ToString()`. The compiler-synthesised `object.ToString()` for a
`sealed class` returns the fully qualified type name — `CryptoExchanges.Net.Okx.OkxOptions` — and
does NOT enumerate property values. (Synthesised `ToString()` that emits property values is a
`record` behaviour; `OkxOptions` is a plain `class`.) There is therefore no implicit credential
leak from `OkxOptions.ToString()`.

The redaction convention is correctly owned by `ExchangeCredentials.ToString()` (ADR-001), which
already redacts `SecretKey` and `Passphrase` and masks `ApiKey` to its last four characters. Any
log call on a `ExchangeCredentials` instance is safe. `OkxOptions` is consistent with `BybitOptions`
(which also has no `ToString()` override and is a plain class), so this is not a regression.

### Signing Integrity

No signing pipeline, no handlers, no `MarkSigned()` usage. Not applicable at this task scope.

### Query String Safety

No URL construction or query parameter building. Not applicable at this task scope.

### Input Validation

No public methods accept user-supplied strings that flow into HTTP requests. `ToCredentials()` is
the only public method added, and it hands control to the validated Core type.

### Secret Management Expansion

`OkxOptions` follows the established options-class pattern (same as `BinanceOptions`,
`BybitOptions`). No new credential source (env vars, config file, etc.) is introduced in this task.

### Serialization Safety

`GlobalUsings.cs` adds `System.Text.Json` and `System.Text.Json.Serialization` as global usings.
These are present in the Bybit project identically and are needed for future JSON DTOs. No
`JsonDocument.Parse()`, `ReadFromJsonAsync<T>`, or serialization of the options class is present
in this diff.

### Rate Limiting

No HTTP client or `IRateLimitGate` registration is introduced. Rate limiting is out of scope for
this scaffold task.

### `DynamicProxyGenAssembly2` IVT

`InternalsVisibleTo` for `DynamicProxyGenAssembly2` (unsigned) is present. This follows the Bybit
pattern exactly and is standard practice for NSubstitute/Castle mocking in test assemblies. No
security concern.

---

## Findings

### Finding: OkxOptions has no ToString() redaction override
- **Severity**: LOW
- **Confidence**: 45
- **File**: `src/CryptoExchanges.Net.Okx/OkxOptions.cs:8-40`
- **Category**: Security
- **Verdict**: PASS (non-blocking concern, consistent with existing Bybit precedent)
- **Issue**: `OkxOptions` holds three credential fields (`ApiKey`, `SecretKey`, `Passphrase`) and
  does not override `ToString()`. For a plain `sealed class` this is benign — `object.ToString()`
  returns only the type name, not property values. However, a future refactor to `record` or
  a naive structured-logger that reflects on all properties could surface credentials. The concern
  is amplified slightly compared to `BybitOptions` because OKX has a third credential
  (`Passphrase`) that is novel to this codebase.
- **Fix (non-blocking, recommended for TASK-011 or a housekeeping task)**: Add a `ToString()`
  override redacting `SecretKey` and `Passphrase` and masking `ApiKey`, mirroring the pattern
  already established on `ExchangeCredentials`:
  ```csharp
  public override string ToString()
      => $"{nameof(OkxOptions)} {{ BaseUrl = {BaseUrl}, ApiKey = {Mask(ApiKey)}, "
       + $"SecretKey = [REDACTED], Passphrase = [REDACTED], TimeoutSeconds = {TimeoutSeconds} }}";

  private static string Mask(string v) => v.Length <= 4 ? "****" : $"****{v[^4..]}";
  ```
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:59-64`

  This concern is explicitly scoped as non-blocking. `BybitOptions` has the same gap and was
  approved without remediation. The redaction convention lives in `ExchangeCredentials` per ADR-001.
  Score of 45 confidence keeps this as a CONCERN, not a REJECT.

---

## Summary

- PASS: Credential field declarations — no `[JsonInclude]`, no serialization path, no hardcoded
  secrets, no transmission of `SecretKey` or `Passphrase`.
- PASS: `ToCredentials()` — safely delegates to validated `ExchangeCredentials`; no signing logic.
- PASS: `GlobalUsings.cs` — JSON usings present but no serialization of options class or credentials.
- PASS: `csproj` — Core + Http refs only; correct `InternalsVisibleTo` set matching Bybit; no
  extra suppressed security warnings.
- PASS: `ToString()` implicit behaviour — `sealed class` (not `record`) means `object.ToString()`
  emits only the type name; no credential leak from default behaviour.
- CONCERN: No `ToString()` redaction override on `OkxOptions` — consistent with `BybitOptions`
  precedent; `ExchangeCredentials` owns the redaction per ADR-001; recommended as non-blocking
  follow-up for TASK-011 or housekeeping. (confidence: 45/100, non-blocking)

---

## Final Verdict

**APPROVED**

All hard security checks pass. The single concern (no `ToString()` redaction override) is
non-blocking, mirrors existing Bybit precedent, and is mitigated by the fact that `OkxOptions` is
a plain `class` whose implicit `ToString()` does not enumerate properties. The redaction convention
is correctly owned by `ExchangeCredentials`. No blocking issues found.
