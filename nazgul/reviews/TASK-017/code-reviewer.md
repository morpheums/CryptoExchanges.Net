# Code Review — TASK-017: Bitget project scaffold + passphrase options + DI seam stub

**Reviewer**: Code Reviewer (automated)
**Date**: 2026-06-18
**Branch**: feat/m4-bitget
**Build**: `dotnet build CryptoExchanges.Net.sln` → 0 Warning(s), 0 Error(s) (verified)

---

## Findings

### Finding: Missing `<exception>` doc on `ToCredentials()` vs OKX template
- **Severity**: LOW
- **Confidence**: 72
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetOptions.cs:23-25`
- **Category**: Documentation
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `OkxOptions.ToCredentials()` carries an `<exception cref="ArgumentException">` tag documenting that it throws when any credential field is empty/whitespace. `BitgetOptions.ToCredentials()` omits that tag. Because `Passphrase` defaults to `string.Empty` (not `null`) and `ExchangeCredentials..ctor` calls `ThrowIfNullOrWhiteSpace` on non-null passphrases, calling `ToCredentials()` before setting `Passphrase` (or `ApiKey`/`SecretKey`) will throw — and the caller has no doc warning. The build confirms this does not cause a compile failure (CS1591 is suppressed anyway), but it is an observable divergence from the OKX template the task mandates mirroring.
- **Fix**: Add `/// <exception cref="ArgumentException"><see cref="ApiKey"/> or <see cref="SecretKey"/> is empty/whitespace, or <see cref="Passphrase"/> is empty/whitespace (Bitget always requires a passphrase for signed requests).</exception>` to `ToCredentials()` to match `OkxOptions.cs:34-37`.
- **Pattern reference**: `src/CryptoExchanges.Net.Okx/OkxOptions.cs:34-37`

### Finding: `ToCredentials()` passes `string.Empty` as passphrase (throws on default options)
- **Severity**: LOW
- **Confidence**: 68
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetOptions.cs:24-25`
- **Category**: Correctness
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `Passphrase` defaults to `string.Empty`. `ExchangeCredentials..ctor` checks `if (passphrase is not null) ThrowIfNullOrWhiteSpace(passphrase)`, so passing `""` throws `ArgumentException`. A caller who builds `new BitgetOptions()` and calls `ToCredentials()` without setting `Passphrase` receives a runtime exception. This is identical behaviour to OKX (same default, same constructor guard), and the OKX comments in `ServiceCollectionExtensions.cs:41` and `OkxClientComposer.cs:86` explicitly note this and route around it — so the pattern is accepted. Confidence is low because the task explicitly says "No signing yet" and this throws will only surface in future wiring code. The doc gap (Finding 1) is the more actionable issue.
- **Fix**: No code change required at this stage (matches OKX posture exactly); addressed with the `<exception>` doc from Finding 1.
- **Pattern reference**: `src/CryptoExchanges.Net.Okx/ServiceCollectionExtensions.cs:41`

---

## Checklist

### Scaffold correctness
- PASS: `BaseUrl` defaults to `https://api.bitget.com` — correct per task AC.
- PASS: `ApiKey`, `SecretKey`, `Passphrase` all present with `string.Empty` defaults — matches OKX shape.
- PASS: No `ReceiveWindow` — correctly absent.
- PASS: `TimeoutSeconds` defaults to 30 — matches OKX.
- PASS: `ToCredentials()` → `new ExchangeCredentials(ApiKey, SecretKey, Passphrase)` — constructor signature confirmed (`string apiKey, string secretKey, string? passphrase = null`); `string` implicitly assignable to `string?`.
- PASS: `ExchangeId.Bitget` confirmed in `Core/Enums/Enums.cs:138`; no Core edit in this task.

### Project file
- PASS: `NoWarn` set is byte-for-byte identical to OKX (`CA1812;CA1056;CA1031;CA2000;CA1305;CA1032;CS1591`).
- PASS: `ProjectReference` to Core and Http only — confirmed by `dotnet list` in task notes and by reading csproj directly.
- PASS: `InternalsVisibleTo` entries mirror OKX: `Tests.Unit`, `Tests.Integration`, `DynamicProxyGenAssembly2`.
- PASS: `DeltaMapper 1.2.0`, `Microsoft.Extensions.Http/Options/DependencyInjection.Abstractions 10.0.*` — identical to OKX.
- PASS: No Microsoft.Extensions.DependencyInjection (non-Abstractions) reference — correct per ADR-001.

### GlobalUsings
- PASS: Identical to OKX `GlobalUsings.cs` (System.Text.Json, System.Text.Json.Serialization, Core.Enums, Core.Interfaces, Core.Models).

### Solution file
- PASS: Project GUID `{975387A7-B48B-4FD2-96F0-238BA8580CBC}` registered in all six platform/configuration slots (Debug/Release × Any CPU/x64/x86).
- PASS: Nested under `{827E0CD3-B72D-47B6-A68D-7590B98EB39B}` which is confirmed as the `src` solution folder.

### XML documentation
- PASS: All 6 public members (`BaseUrl`, `ApiKey`, `SecretKey`, `Passphrase`, `TimeoutSeconds`, `ToCredentials`) carry `<summary>` — CS1591 would fire without the `NoWarn` only if any were missing; all are present.
- PASS: Class-level `<summary>` present.
- CONCERN (low): `ToCredentials()` missing `<exception>` tag (see Finding 1, confidence 72, non-blocking).

### Roslyn / nullable
- PASS: Build clean, 0 warnings, 0 errors.
- PASS: All properties are non-nullable `string` with `= string.Empty` defaults — no nullable annotation issues.
- PASS: `sealed class` — correct for mutable config object (mirrors `BinanceOptions` / `OkxOptions`).
- PASS: No `#pragma warning disable` added; no unsuppressed suppressions.

### Naming & style
- PASS: Class name `BitgetOptions`, namespace `CryptoExchanges.Net.Bitget` — correct.
- PASS: Properties `PascalCase` throughout.
- PASS: No private fields in this file; naming convention N/A.

### Async / CT / disposables
- PASS: No async code in this scaffold; N/A.

### Exception handling
- PASS: No `catch` blocks; N/A.

---

## Summary

- PASS: `BitgetOptions` public surface — all 5 properties and `ToCredentials()` present with correct defaults and types.
- PASS: `CryptoExchanges.Net.Bitget.csproj` — identical `NoWarn`, refs, and IVT to OKX template.
- PASS: `GlobalUsings.cs` — byte-for-byte mirror of OKX.
- PASS: Solution file entries — all platform configs present, correctly nested under `src` folder.
- PASS: Build — 0 warnings, 0 errors confirmed.
- CONCERN: `ToCredentials()` missing `<exception>` doc tag vs OKX template (confidence: 72/100, non-blocking).
- CONCERN: `ToCredentials()` throws on default-constructed `BitgetOptions` (same posture as OKX; no code change needed; doc gap is the action item — confidence: 68/100, non-blocking).

---

## Final Verdict

**APPROVED**

No blocking findings. Both concerns are low-confidence (< 80) and low-severity. The `<exception>` doc gap on `ToCredentials()` is the only meaningful divergence from the OKX template and does not affect build correctness or runtime behaviour of the scaffold itself. The implementation faithfully mirrors the OKX project structure and satisfies all acceptance criteria.
