---
reviewer: security-reviewer
task: TASK-056
verdict: APPROVE
confidence: 97
---

# Security Review — TASK-056

## Summary

TASK-056 introduces a structural scaffold for the KuCoin exchange package: `KucoinOptions`, `KucoinSymbolFormat`, `GlobalUsings`, csproj, and smoke tests. No signing implementation is present. The scaffold correctly follows the OKX pattern and introduces no credential exposure, serialization leakage, or hardcoded secrets.

## Findings

### KucoinOptions lacks a ToString() override (SEVERITY: LOW | confidence: 72%)

`KucoinOptions` has public `ApiKey`, `SecretKey`, and `Passphrase` string properties with no `ToString()` override. The default record/class `ToString()` on a `sealed class` in C# is the type name only (`CryptoExchanges.Net.Kucoin.KucoinOptions`), so no credential values are emitted by default. However, if a caller ever logs or interpolates a `KucoinOptions` instance directly, the default `ToString()` would not expose secrets — but if a future developer adds `[DebuggerDisplay]` or reflection-based diagnostics, the fields become visible. The OKX equivalent `OkxOptions` also lacks a `ToString()` override, so this is a consistent pattern gap across the codebase, not a regression. The downstream `ExchangeCredentials` record (`src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:59-61`) does provide a proper redacting `ToString()`, which is the object that holds credentials at runtime once `ToCredentials()` is called. Non-blocking at this scope.

**Verdict for this finding**: CONCERN (non-blocking, confidence: 72%)

**Fix**: Add a `ToString()` override to `KucoinOptions` that redacts `SecretKey` and `Passphrase` and masks `ApiKey` (matching the pattern in `ExchangeCredentials.cs:59-64`). This is a hardening step, not a blocking issue for the scaffold.

### No [JsonPropertyName] or serialization attributes on credential fields (SEVERITY: PASS)

`KucoinOptions.ApiKey`, `SecretKey`, and `Passphrase` carry no `[JsonPropertyName]`, `[JsonInclude]`, or `[DataMember]` attributes. The class is a plain `sealed class`, not a record or DTO, so `System.Text.Json` will not serialize it by default without explicit configuration. No serialization path is introduced by this diff.

### No hardcoded secrets in any scaffolded file (SEVERITY: PASS)

All three credential fields (`ApiKey`, `SecretKey`, `Passphrase`) default to `string.Empty`. The smoke tests only assert empty defaults. No real or synthetic keys appear anywhere in the diff.

### ToCredentials() soundness (SEVERITY: PASS)

`KucoinOptions.ToCredentials()` delegates directly to `new ExchangeCredentials(ApiKey, SecretKey, Passphrase)`. `ExchangeCredentials` validates all three arguments via `ArgumentException.ThrowIfNullOrWhiteSpace` (for `ApiKey` and `SecretKey`) and a null-guarded whitespace check for `Passphrase` (`src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:42-45`). The passphrase is passed as a non-null `string`, so the non-null branch validation fires — meaning an empty/whitespace passphrase would throw, which is correct for KuCoin where a passphrase is always required for signed endpoints. The delegation is sound.

### GlobalUsings imports System.Text.Json.Serialization (SEVERITY: LOW | confidence: 55%)

`GlobalUsings.cs` imports `System.Text.Json.Serialization` globally for the Kucoin assembly. This is appropriate for future DTO work and is identical to the pattern in other exchange packages. There is no evidence this causes credential types to be decorated with serialization attributes. No concern at this scope.

### Smoke tests do not exercise ToCredentials() with valid credentials (SEVERITY: LOW | confidence: 60%)

`ScaffoldSmokeTests.cs` tests only defaults (empty strings). There is no test asserting that `ToCredentials()` throws `ArgumentException` when called with empty credentials, or that it succeeds and returns a properly populated `ExchangeCredentials`. This is a test coverage gap, not a security vulnerability. Future signing tasks should include a `ToCredentials_WithValidInputs_ReturnsCredentials` and a `ToCredentials_WithEmptyApiKey_Throws` test. Non-blocking.

## Verdict: APPROVE

The scaffold introduces no credential exposure paths, no serialization leakage, no hardcoded secrets, and no bypass of the `ExchangeCredentials` validation layer. The `ToCredentials()` method correctly delegates validation to `ExchangeCredentials`, which already provides a redacting `ToString()`. The single non-blocking concern — absence of a `ToString()` override on `KucoinOptions` itself — is a hardening gap shared with `OkxOptions` and does not constitute a blocking security defect for a scaffold task. All in-scope security checks pass.
