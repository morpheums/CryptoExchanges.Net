# Code Review: TASK-010 — OKX Project Scaffold + Passphrase Options + DI Seam Stub

**Branch**: `feat/m3-okx`
**Reviewer**: Code Reviewer
**Scope**: Pure structural scaffolding — `CryptoExchanges.Net.Okx.csproj`, `GlobalUsings.cs`, `OkxOptions.cs`, `CryptoExchanges.Net.sln`

---

## Build Verification

- `dotnet build CryptoExchanges.Net.sln` — Build succeeded. 0 Warning(s), 0 Error(s).
- `dotnet test --filter 'Category!=Integration'` — 241 tests pass (80 Bybit, 93 Core, 12 Http, 11 DI, 45 Binance integration). 0 failures.
- `dotnet list src/CryptoExchanges.Net.Okx reference` — Core + Http only. Layer chain preserved.

---

## Findings

### Finding 1: `ToCredentials()` XML `<exception>` doc parenthetical is slightly misleading
- **Severity**: LOW
- **Confidence**: 72
- **File**: `src/CryptoExchanges.Net.Okx/OkxOptions.cs:35-37`
- **Category**: Code Quality
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: The exception doc reads: "or `Passphrase` is empty/whitespace (OKX always requires a passphrase for signed requests)". The parenthetical implies the passphrase check is contextual (signed requests only), but the actual behavior is unconditional: `ExchangeCredentials(apiKey, secretKey, passphrase)` uses `if (passphrase is not null) ArgumentException.ThrowIfNullOrWhiteSpace(passphrase)`. Since `OkxOptions.Passphrase` is a non-nullable `string` defaulting to `string.Empty`, calling `ToCredentials()` with the default empty passphrase always throws `ArgumentException` — there is no "signed vs. public" branch inside `ToCredentials()`. The parenthetical is misleading to a reader who might think the throw is conditional on making a signed request. The behavior is correct; only the explanatory note is imprecise.
- **Fix**: Remove the parenthetical. Change "or `<see cref="Passphrase"/>` is empty/whitespace (OKX always requires a passphrase for signed requests)" to "or `<see cref="Passphrase"/>` is empty/whitespace."
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:36-39` — the Core doc accurately states "a non-null passphrase is empty/whitespace" without any contextual condition.

---

### Finding 2: `OkxOptions.Passphrase` property doc and `ToCredentials()` create a mild cross-read contradiction
- **Severity**: LOW
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Okx/OkxOptions.cs:19-24`
- **Category**: Code Quality
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `OkxOptions.Passphrase` docs say "Leave empty when only public market-data endpoints are used." This is correct in isolation — the options object can be constructed with an empty passphrase for public endpoint use. However, a reader who reads the `Passphrase` property doc and then calls `ToCredentials()` expecting credentials for public endpoint use will get an `ArgumentException`. The two docs are individually accurate but read together they imply "empty passphrase is fine → I can call `ToCredentials()` for public use", which is wrong.
- **Fix**: Add a cross-reference sentence to the `Passphrase` property doc: "When calling `ToCredentials()`, a non-empty value is required." This is additive only; no behavior change.
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:22-25` — the Core record's `Passphrase` property doc is consistent with its constructor's validation behavior.

---

## Summary

- PASS: **Build** — 0 warnings, 0 errors under `TreatWarningsAsErrors=true`.
- PASS: **Unit tests** — 241 tests pass, no regressions.
- PASS: **Project references** — Core + Http only. No Binance/Bybit/DI cross-references.
- PASS: **`OkxOptions` shape** — `sealed class`, mutable `{ get; set; }` properties, correct defaults, exact mirror of `BybitOptions`. No `ReceiveWindow` (correct — OKX uses ISO-8601 timestamp auth). `Passphrase` field added and documented.
- PASS: **XML documentation** — All public members have `/// <summary>`. `ToCredentials()` has `<returns>` and `<exception cref="ArgumentException">`. CS1591 suppressed in `NoWarn`. Project is CS1591-clean.
- PASS: **`ToCredentials()` delegation** — Correctly delegates to `new ExchangeCredentials(ApiKey, SecretKey, Passphrase)` with no reimplemented validation logic.
- PASS: **`ExchangeCredentials` passphrase exception type** — `ArgumentException` declared in the doc is correct. `ArgumentException.ThrowIfNullOrWhiteSpace` throws `ArgumentException`; the non-nullable `string` default of `string.Empty` correctly triggers the non-null branch guard in `ExchangeCredentials`.
- PASS: **`csproj` structure** — `NoWarn` list identical to Bybit with justification comment. `InternalsVisibleTo` covers both future test projects and `DynamicProxyGenAssembly2`. Package references are an exact mirror of Bybit.
- PASS: **`GlobalUsings.cs`** — Byte-for-byte equivalent to `CryptoExchanges.Net.Bybit/GlobalUsings.cs`. Auth excluded intentionally and used explicitly in `OkxOptions.cs` only.
- PASS: **Solution file** — OKX project registered with GUID `{179D89FC-030B-48CD-9893-D33E87FF1135}`. Full Debug/Release × Any CPU/x64/x86 platform config entries present. GUID consistent throughout; no mismatch between project declaration and platform config block. Project nested under `{827E0CD3}` (the `src` solution folder).
- PASS: **Guards N/A** — `OkxOptions` is a mutable config object; no argument guards required on property setters. Mirrors `BybitOptions` pattern.
- CONCERN: **`ToCredentials()` exception doc parenthetical** — Misleading conditional phrasing. See Finding 1 (confidence: 72/100, non-blocking).
- CONCERN: **`Passphrase` property doc cross-reference** — "Leave empty for public endpoints" does not warn that `ToCredentials()` will still throw. See Finding 2 (confidence: 70/100, non-blocking).

---

## Final Verdict

**APPROVED**

The scaffolding is correct, complete, and clean. Build passes with zero warnings under `TreatWarningsAsErrors=true`. All acceptance criteria are met: `OkxOptions` is a properly documented `sealed class` with the required fields including `Passphrase`; `ToCredentials()` correctly delegates to `ExchangeCredentials` without reimplementing validation; project references are Core + Http only; `ExchangeId.Okx` is reused with no Core enum edit; the `.csproj`, `GlobalUsings.cs`, and `.sln` entries are faithful mirrors of the Bybit template. The two concerns are minor XML doc clarity issues that do not affect correctness, compilability, or runtime behavior.
