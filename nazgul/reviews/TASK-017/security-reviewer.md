# Security Review — TASK-017
**Reviewer**: Security Reviewer (automated)
**Date**: 2026-06-18
**Task**: Bitget project scaffold + passphrase options + DI seam stub
**Branch**: feat/m4-bitget
**Verdict**: APPROVED

---

## Scope

Pure scaffolding task. Files introduced: `BitgetOptions.cs`, `GlobalUsings.cs`,
`CryptoExchanges.Net.Bitget.csproj`, plus solution registration. No signing code,
no HTTP client, no handlers.

---

## Findings

### Finding: BitgetOptions.ToCredentials() passes string.Empty passphrase — not null — which throws at runtime

- **Severity**: LOW
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetOptions.cs:24-25`
- **Category**: Credential safety / API contract
- **Verdict**: CONCERN (non-blocking — confidence < 80, and identical behavior exists in OkxOptions)
- **Issue**: `Passphrase` defaults to `string.Empty`. `ToCredentials()` passes it directly to
  `ExchangeCredentials(apiKey, secretKey, passphrase)`. The `ExchangeCredentials` constructor
  treats a non-null passphrase as requiring non-whitespace content
  (`ArgumentException.ThrowIfNullOrWhiteSpace(passphrase)` at
  `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:44-45`), so calling `ToCredentials()`
  when `Passphrase` has not been configured will throw `ArgumentException` at runtime. This is not
  a security vulnerability — it is a misconfiguration that fails loudly — but it does make
  `ToCredentials()` unusable for the "public market data only, no passphrase" use case.
- **Pre-existing parity**: `OkxOptions` has exactly the same default and the same `ToCredentials()`
  body. The OKX signing path explicitly avoids calling `ToCredentials()` and instead gates directly
  on `string.IsNullOrEmpty(o.Passphrase)` (see `OkxClientComposer.cs:88` and
  `ServiceCollectionExtensions.cs:60`). Bitget's TASK-018/019 signing path should adopt the same
  gate pattern — this does not block scaffolding.
- **Fix (deferred to TASK-018)**: In the Bitget service-collection extension, guard the signing
  handler registration with `string.IsNullOrEmpty(o.Passphrase)` rather than calling
  `ToCredentials()`. Alternatively, change the default of `Passphrase` to `null` (matching the
  `string?` semantics of `ExchangeCredentials.Passphrase`) so that `ToCredentials()` correctly
  signals "no passphrase" to the constructor.
- **Pattern reference**: `src/CryptoExchanges.Net.Okx/Internal/OkxClientComposer.cs:88`

---

### Finding: BitgetOptions has no ToString() redaction

- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetOptions.cs:5-26`
- **Category**: Credential safety
- **Verdict**: CONCERN (non-blocking — confidence < 80; identical posture as OkxOptions)
- **Issue**: `BitgetOptions` does not override `ToString()`. Accidental logging of the options
  object (e.g., `logger.LogDebug("{opts}", options)`) would emit all three secret fields in plain
  text. `ExchangeCredentials` correctly redacts via its `ToString()` override, but that is only
  called when credentials have already been extracted.
- **Mitigating context**: `OkxOptions` (the direct template) also has no `ToString()` override,
  so this is consistent with the established codebase posture. The concern is at the
  project-conventions level, not this task specifically. A future ADR could mandate `ToString()`
  redaction on all options classes.
- **Fix (optional, post-milestone)**: Add `public override string ToString() =>
  $"BitgetOptions {{ ApiKey = {Mask(ApiKey)}, SecretKey = [REDACTED], Passphrase = [REDACTED] }}"`.
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:59-61`

---

## Checklist Results

| Check | Result | Notes |
|-------|--------|-------|
| SecretKey stored outside signing handler / service | PASS | No signing code in this task |
| ApiKey / SecretKey logged or in exception messages | PASS | No logging in scaffold |
| JsonInclude / serialization attributes on secrets | PASS | No JSON attributes anywhere in BitgetOptions |
| SecretKey transmitted directly | PASS | No HTTP code introduced |
| BaseUrl default is HTTPS | PASS | `https://api.bitget.com` confirmed |
| Premature / incorrect HMAC code | PASS | No crypto code introduced |
| ToCredentials() leaks secret | PASS | Delegates to ExchangeCredentials which has its own redacted ToString |
| Hardcoded credentials | PASS | No hardcoded credentials |
| CA1031 (general exception catch) in NoWarn | PASS | Identical to OKX NoWarn; expected for HTTP client library pattern |
| InternalsVisibleTo scope | PASS | Limited to Tests.Unit, Tests.Integration, DynamicProxyGenAssembly2 (NSubstitute); no production assembly |
| Cross-exchange project reference | PASS | ProjectReferences: Core + Http only, confirmed |
| No signing / rate-limit gate required this task | PASS | Scaffolding only; gate arrives in TASK-018/019 |

---

## Summary

- PASS: Credential fields (ApiKey, SecretKey, Passphrase) — no logging, no serialization, no direct transmission, no hardcoded values.
- PASS: BaseUrl default — confirmed HTTPS (`https://api.bitget.com`).
- PASS: No HMAC / signing / crypto code introduced — task is pure scaffolding as required.
- PASS: ToCredentials() — passes secrets to ExchangeCredentials, which has its own redaction; no leakage path beyond the ExchangeCredentials boundary.
- PASS: InternalsVisibleTo — scoped to test assemblies and NSubstitute DynamicProxy only.
- PASS: NoWarn set — identical to OKX; no security-relevant analyzer bypassed beyond the expected HTTP-library suppressions.
- CONCERN: ToCredentials() passing string.Empty passphrase — pre-existing OkxOptions behavior; fails loudly (ArgumentException) rather than silently; fix deferred to TASK-018 signing path. (confidence: 70/100, non-blocking)
- CONCERN: Missing ToString() redaction on BitgetOptions — identical posture as OkxOptions; non-blocking convention debt. (confidence: 55/100, non-blocking)

---

## Final Verdict

**APPROVED**

No findings with severity HIGH or MEDIUM at confidence >= 80. Both concerns are pre-existing behaviors shared with OkxOptions (the explicit pattern reference for this task) and carry no active exploitation path at the scaffold stage. The signing path that would consume these credentials does not exist yet; the two concerns are flagged for the TASK-018/019 implementer to handle at the point of signing wire-up.
