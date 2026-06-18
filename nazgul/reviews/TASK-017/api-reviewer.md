# API Review — TASK-017: Bitget project scaffold + passphrase options + DI seam stub

**Reviewer**: API Reviewer
**Date**: 2026-06-18
**Branch**: feat/m4-bitget
**Verdict**: APPROVED

---

## Scope

Pure scaffolding: new `CryptoExchanges.Net.Bitget` project (`csproj`, `GlobalUsings.cs`, `BitgetOptions.cs`) plus solution registration. No Core changes. No interface additions. No existing API surface modified.

---

## Findings

### Finding: `ToCredentials()` will throw when called with default (empty) `Passphrase`

- **Severity**: MEDIUM
- **Confidence**: 72
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetOptions.cs:24` / `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:44-45`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence 72 < 80)
- **Issue**: `BitgetOptions.Passphrase` defaults to `string.Empty`. `ExchangeCredentials` constructor treats a non-null passphrase as "present" and calls `ArgumentException.ThrowIfNullOrWhiteSpace(passphrase)`, so passing `string.Empty` throws at runtime even before any signing occurs. Callers who call `ToCredentials()` without setting a passphrase get an unguided `ArgumentException` rather than a clear configuration error. This is a pre-existing pattern shared with `OkxOptions` (identical shape, identical trap), so it is not a regression introduced by this diff; however the same latent issue is now present in the new type.
- **Fix**: Either default `Passphrase` to `null` (breaking the `string` property type contract — requires changing to `string?`) or coerce `string.Empty` to `null` inside `ToCredentials()`: `=> new(ApiKey, SecretKey, string.IsNullOrWhiteSpace(Passphrase) ? null : Passphrase)`. The second option is entirely non-breaking and self-contained to this file. Preferred: align with the OKX fix when that is addressed (tracking issue, not a blocker for this scaffolding task).
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:40-45` (the constructor validation)

---

### Finding: `PackageTags` in `Directory.Build.props` does not include "bitget"

- **Severity**: LOW
- **Confidence**: 65
- **File**: `Directory.Build.props:16`
- **Category**: NuGet Conventions
- **Verdict**: CONCERN (non-blocking — confidence 65 < 80)
- **Issue**: `Directory.Build.props` `PackageTags` reads `crypto;exchange;trading;sdk;binance;coinbase;bybit;bitcoin;ethereum`. "bitget" and "okx" are absent (OKX was added before this tag was set or is similarly unlisted). NuGet discoverability for the new package is mildly impaired.
- **Fix**: Add `okx;bitget` to the `PackageTags` string in `Directory.Build.props`. This is a global metadata change; low urgency since the package is `0.1.0-preview.1`.
- **Pattern reference**: `Directory.Build.props:16`

---

## Passing Checks

- **PASS: `BitgetOptions` public surface** — `BaseUrl`, `ApiKey`, `SecretKey`, `Passphrase`, `TimeoutSeconds`, `ToCredentials()` exactly mirrors `OkxOptions`. No `ReceiveWindow` (correct — Bitget does not use it). All members present per acceptance criteria.
- **PASS: `sealed class` modifier** — matches OKX/Binance options pattern; prevents unintended subclassing.
- **PASS: XML documentation — lean comments (ADR-001 conv 7)** — exactly one concise `<summary>` per public member. No `<remarks>` essays, no `<b>` tags, no `<exception>` XML, no redundant `<returns>` on `ToCredentials()`. This is strictly leaner than `OkxOptions` (which has verbose `<remarks>` and `<exception>` blocks on `ToCredentials()`) and is the CORRECT new convention. No concern raised.
- **PASS: `csproj` package metadata** — `PackageId`, `Description`, `RootNamespace`, `AssemblyName` all set and consistent. License, authors, version, and repo URL inherited from `Directory.Build.props` (same as OKX). `GenerateDocumentationFile` inherited from `Directory.Build.props:8`. No `IsPackable=false` required (this is a library, not a test/sample project).
- **PASS: Project references** — Core + Http only; no cross-exchange references; no DI project reference (ADR-001 compliant).
- **PASS: Package references** — `DeltaMapper 1.2.0`, `Microsoft.Extensions.DependencyInjection.Abstractions 10.0.*`, `Microsoft.Extensions.Http 10.0.*`, `Microsoft.Extensions.Options 10.0.*` — identical set to OKX. Correct for a scaffold that will later add signing and DI.
- **PASS: `InternalsVisibleTo`** — Test assemblies (`Tests.Unit`, `Tests.Integration`) and `DynamicProxyGenAssembly2` (NSubstitute). No IVT granted to consumer or DI packages. Justified pattern mirrors OKX exactly.
- **PASS: `GlobalUsings.cs`** — character-for-character match with OKX global usings (comment header updated to "Bitget"). Correct.
- **PASS: Solution registration** — Bitget project GUID added with full Debug/Release × Any CPU/x64/x86 configuration matrix and nested under solution folder `827E0CD3` (the `src` folder), mirroring OKX registration.
- **PASS: No Core edits** — `ExchangeId.Bitget` (from TASK-016) is used; no interface changes; no model changes; no breaking changes of any kind.
- **PASS: Namespace** — `CryptoExchanges.Net.Bitget` matches assembly name and package ID.
- **PASS: No `ReceiveWindow`** — correctly absent; this is a Binance-specific concept.
- **PASS: Forward extensibility** — Options shape slots directly into the signing/DI tasks planned for later milestones. `ToCredentials()` delegates to `ExchangeCredentials` which already documents Bitget as a passphrase exchange.

---

## Summary

| Item | Verdict | Confidence |
|------|---------|-----------|
| `ToCredentials()` throws on empty default Passphrase | CONCERN | 72 — non-blocking |
| `PackageTags` missing "bitget" | CONCERN | 65 — non-blocking |
| `BitgetOptions` public surface matches OkxOptions pattern | PASS | — |
| Lean XML comments (ADR-001 conv 7) | PASS | — |
| `csproj` package metadata | PASS | — |
| Project/package references | PASS | — |
| `InternalsVisibleTo` hygiene | PASS | — |
| `GlobalUsings.cs` | PASS | — |
| Solution registration | PASS | — |
| No breaking changes to existing API | PASS | — |

## Final Verdict

**APPROVED**

No blocking findings. Both concerns are non-blocking (confidence below threshold) and are pre-existing patterns shared with OKX scaffolding. The `ToCredentials()` passphrase coercion concern should be tracked for the OKX+Bitget signing tasks where `ToCredentials()` is first exercised in production paths.
