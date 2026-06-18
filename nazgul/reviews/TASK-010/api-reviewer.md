# API Review — TASK-010: OKX Project Scaffold + Passphrase Options + DI Seam Stub

**Reviewer**: api-reviewer
**Branch**: feat/m3-okx
**Commit**: af642795acfca20118a0f73c8a87c0de7c615b73
**Date**: 2026-06-18
**Verdict**: APPROVED
**Confidence**: 96/100

---

## Scope

New public API surface introduced by this task:

- `CryptoExchanges.Net.Okx.OkxOptions` (sealed class, 5 properties + 1 method)
- `CryptoExchanges.Net.Okx.csproj` (new NuGet-packable project)
- `CryptoExchanges.Net.Okx.GlobalUsings.cs` (file-scoped global usings, not public API)
- `CryptoExchanges.Net.sln` (solution registration)

No Core interface, model, enum, or exception changes. No breaking changes to any existing public surface.

---

## Findings

### Finding: OkxOptions shape mirrors BybitOptions correctly with deliberate Passphrase addition
- **Severity**: N/A (PASS)
- **Confidence**: 99/100
- **File**: `src/CryptoExchanges.Net.Okx/OkxOptions.cs:8-40`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. `BaseUrl`, `ApiKey`, `SecretKey`, `TimeoutSeconds` exactly match `BybitOptions` (`src/CryptoExchanges.Net.Bybit/BybitOptions.cs:1-22`). `Passphrase` is added as the deliberate OKX-specific 3rd credential. All properties are `string`/`int` with `{ get; set; }` consistent with the sealed class Options convention.

---

### Finding: ReceiveWindow correctly omitted for OKX
- **Severity**: N/A (PASS)
- **Confidence**: 99/100
- **File**: `src/CryptoExchanges.Net.Okx/OkxOptions.cs`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. OKX authenticates with an ISO-8601 UTC timestamp baked into the HMAC prehash string (`OK-ACCESS-TIMESTAMP` header). There is no server-enforced recv-window concept analogous to Bybit's `recv-window` header. Including a `ReceiveWindow` property would be dead and misleading configuration. The omission is correct and intentional, as documented in the implementation notes.

---

### Finding: ToCredentials() — passphrase forwarding behavior with empty-string default
- **Severity**: MEDIUM
- **Confidence**: 82/100
- **File**: `src/CryptoExchanges.Net.Okx/OkxOptions.cs:38-39`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking)
- **Issue**: `OkxOptions.Passphrase` defaults to `string.Empty`. `ToCredentials()` calls `new ExchangeCredentials(ApiKey, SecretKey, Passphrase)`. The `ExchangeCredentials` constructor accepts `string? passphrase = null` and, when passphrase is non-null, guards with `ArgumentException.ThrowIfNullOrWhiteSpace(passphrase)` (`src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:44-45`). An empty string is non-null, so `ToCredentials()` called on a default `OkxOptions()` (Passphrase = `""`) will throw `ArgumentException` before it ever throws on `ApiKey`/`SecretKey`. This is technically correct behaviour for the signed-request case (the doc says it throws when passphrase is empty/whitespace), but it means a caller who wants to use public-only endpoints cannot use `ToCredentials()` at all — they must set a passphrase even though the XML doc says "Leave empty when only public market-data endpoints are used." The doc and the runtime behaviour are in mild tension: the XML doc on `Passphrase` says leave it empty for public endpoints, but `ToCredentials()` will throw if you do. This is not a bug (public endpoints don't need credentials at all), but the `ToCredentials()` XML doc exception clause — "OKX always requires a passphrase for signed requests" — could be made even clearer that this method is only meaningful for signed requests, and that callers using only public endpoints should not call it.
- **Fix**: The current wording is acceptable. Optionally, add one sentence to the `ToCredentials()` doc: "Do not call this method when using only public, unauthenticated endpoints." This would remove any ambiguity about the empty-string passphrase throwing at runtime. Not blocking.
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:40-50`

---

### Finding: ToCredentials() naming and discoverability
- **Severity**: LOW
- **Confidence**: 78/100
- **File**: `src/CryptoExchanges.Net.Okx/OkxOptions.cs:38`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking)
- **Issue**: `BybitOptions` does not have a `ToCredentials()` method. `OkxOptions` introduces one. This creates a surface asymmetry — `BybitOptions` callers must construct `ExchangeCredentials` manually, while `OkxOptions` callers can call `ToCredentials()`. The asymmetry is justified (OKX has a 3rd credential that would otherwise be easy to omit), and the task explicitly permitted this convenience. However, if `BybitOptions` later gains a similar method the naming should remain consistent (`ToCredentials()` is correct). No action required for this task.
- **Fix**: No fix required. Track as future consideration: if `BybitOptions` ever adds a credentials helper, use the same name `ToCredentials()`.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/BybitOptions.cs:1-22`

---

### Finding: XML documentation complete and accurate on all public members
- **Severity**: N/A (PASS)
- **Confidence**: 99/100
- **File**: `src/CryptoExchanges.Net.Okx/OkxOptions.cs:5-39`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. All 5 properties and the 1 method carry XML doc comments. The class itself has a summary. `Passphrase` doc correctly states "required for signed/private endpoints" and "Leave empty when only public market-data endpoints are used." `ToCredentials()` documents the return type and the `ArgumentException` condition. Build report confirms 0 warnings under `TreatWarningsAsErrors` + `CS1591` in the NoWarn list (CS1591 suppression consistent with Bybit — the suppression allows partial-doc scenarios in future; current state is fully documented).

---

### Finding: Default values sensible
- **Severity**: N/A (PASS)
- **Confidence**: 99/100
- **File**: `src/CryptoExchanges.Net.Okx/OkxOptions.cs:11,14,17,24,27`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. `BaseUrl = "https://www.okx.com"` is the correct OKX V5 REST API root. `TimeoutSeconds = 30` matches Bybit. `ApiKey`, `SecretKey`, `Passphrase` all default to `string.Empty` consistent with BybitOptions and the "required for signed requests, optional for public" pattern.

---

### Finding: NuGet package metadata complete
- **Severity**: N/A (PASS)
- **Confidence**: 98/100
- **File**: `src/CryptoExchanges.Net.Okx/CryptoExchanges.Net.Okx.csproj:3-7`
- **Category**: NuGet Conventions
- **Verdict**: PASS
- **Issue**: `PackageId`, `Description`, `RootNamespace`, `AssemblyName` all present. `PackageLicenseExpression`, `Authors`, `Version`, `GenerateDocumentationFile` are inherited from `Directory.Build.props` (`Directory.Build.props:8,12,18`). Matches the Bybit and Binance csproj patterns exactly.

---

### Finding: PackageTags in Directory.Build.props does not include "okx"
- **Severity**: LOW
- **Confidence**: 70/100
- **File**: `Directory.Build.props:16`
- **Category**: NuGet Conventions
- **Verdict**: CONCERN (non-blocking)
- **Issue**: The shared `PackageTags` value is `crypto;exchange;trading;sdk;binance;coinbase;bybit;bitcoin;ethereum`. It does not include `okx`. This tag is used for NuGet discovery. Adding `okx` when the OKX package ships publicly would improve discoverability. This is a pre-existing gap (Bybit was added without updating the tags) and is out of scope for a pure scaffold task.
- **Fix**: In a follow-up cleanup task, update `Directory.Build.props:16` to add `okx` (and `bybit` if not yet present) to `PackageTags`. Not blocking for this scaffold task.
- **Pattern reference**: `Directory.Build.props:16`

---

### Finding: Project references — Core + Http only (layer chain preserved)
- **Severity**: N/A (PASS)
- **Confidence**: 99/100
- **File**: `src/CryptoExchanges.Net.Okx/CryptoExchanges.Net.Okx.csproj:11-14`
- **Category**: NuGet Conventions
- **Verdict**: PASS
- **Issue**: None. Only `CryptoExchanges.Net.Core` and `CryptoExchanges.Net.Http` are referenced. No Binance or Bybit cross-references. Confirmed by `dotnet list` output in implementation notes.

---

### Finding: InternalsVisibleTo — test + DynamicProxy only, no DI package (ADR-001 compliant)
- **Severity**: N/A (PASS)
- **Confidence**: 99/100
- **File**: `src/CryptoExchanges.Net.Okx/CryptoExchanges.Net.Okx.csproj:16-23`
- **Category**: API Design / NuGet Conventions
- **Verdict**: PASS
- **Issue**: None. `IVT` granted only to `CryptoExchanges.Net.Okx.Tests.Unit`, `CryptoExchanges.Net.Okx.Tests.Integration`, and `DynamicProxyGenAssembly2`. The DI package is NOT granted IVT, complying with ADR-001. Mirrors Bybit's IVT posture exactly.

---

### Finding: GlobalUsings — Auth namespace NOT included globally (correct)
- **Severity**: N/A (PASS)
- **Confidence**: 98/100
- **File**: `src/CryptoExchanges.Net.Okx/GlobalUsings.cs`
- **Category**: API Design
- **Verdict**: PASS
- **Issue**: None. Auth is referenced only explicitly in `OkxOptions.cs:1`. Mirrors Bybit's `GlobalUsings.cs` (`src/CryptoExchanges.Net.Bybit/GlobalUsings.cs`) exactly. This is the correct posture: Auth usings are file-local where needed, not promoted to assembly-wide global scope.

---

### Finding: Solution file registration
- **Severity**: N/A (PASS)
- **Confidence**: 97/100
- **File**: `CryptoExchanges.Net.sln` (diff lines 9-10, 56-67, 75)
- **Category**: NuGet Conventions
- **Verdict**: PASS
- **Issue**: None. Project is added with a unique GUID `{179D89FC-030B-48CD-9893-D33E87FF1135}`, full Debug/Release x Any CPU/x64/x86 platform config entries, and nested under the `src` solution folder `{827E0CD3-B72D-47B6-A68D-7590B98EB39B}`. The duplicate-GUID block for `{D5E6F7A8-B9C0-1234-ABCD-567890123456}` visible in the diff (removed from one position, added to an earlier position) is a harmless reordering by `dotnet sln add` and does not duplicate any GUID.

---

### Finding: No Core interface, model, enum, or exception changes
- **Severity**: N/A (PASS)
- **Confidence**: 100/100
- **File**: (no Core changes in diff)
- **Category**: Compatibility
- **Verdict**: PASS
- **Issue**: None. `ExchangeId.Okx` already exists at `src/CryptoExchanges.Net.Core/Enums/Enums.cs:134`. No Core edit was required or made. No breaking changes to any existing public surface.

---

## Summary

- PASS: `OkxOptions` public shape — mirrors BybitOptions with deliberate Passphrase addition and deliberate ReceiveWindow omission; correct for OKX's ISO-8601 timestamp signing model
- PASS: `ToCredentials()` — correct name, return type (`ExchangeCredentials`), passphrase forwarding; throws on empty passphrase which is consistent with `ExchangeCredentials` validation contract
- PASS: XML documentation — complete on every public member, doc text accurate including Passphrase semantics
- PASS: Default values — `BaseUrl=https://www.okx.com`, `TimeoutSeconds=30` are correct and sensible
- PASS: NuGet metadata — `PackageId`, `Description`, license, doc generation all present/inherited
- PASS: Project references — Core + Http only, no cross-exchange refs
- PASS: IVT posture — test + DynamicProxy only, ADR-001 compliant
- PASS: GlobalUsings — identical to Bybit, Auth kept file-local
- PASS: Solution registration — unique GUID, full platform configs, correct folder nesting
- PASS: No breaking changes — zero Core interface/model/enum edits
- CONCERN: `ToCredentials()` doc vs empty-passphrase default — mild tension: `Passphrase` property says "leave empty for public endpoints" but `ToCredentials()` throws on empty passphrase; both statements are correct but a single clarifying sentence would remove ambiguity (confidence: 82/100, non-blocking)
- CONCERN: `PackageTags` in `Directory.Build.props` does not include `okx` — pre-existing gap, out of scope for scaffold (confidence: 70/100, non-blocking)

## Final Verdict

**APPROVED**

All public API surface checks pass. The two concerns are non-blocking: one is a minor doc-clarity opportunity and one is a pre-existing metadata gap in a shared build file outside this task's scope. The scaffold is structurally correct, the passphrase addition is well-justified and correctly implemented, and the ReceiveWindow omission is the right call for OKX's authentication model.
