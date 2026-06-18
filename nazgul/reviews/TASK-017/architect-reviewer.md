# Architect Review — TASK-017: Bitget project scaffold + passphrase options + DI seam stub

**Reviewer**: Architect Reviewer (automated)
**Date**: 2026-06-18
**Branch**: feat/m4-bitget
**Task commit**: 9029eab

---

## Scope

Pure scaffolding task: new class library `CryptoExchanges.Net.Bitget` (csproj, GlobalUsings, BitgetOptions) + solution registration. Template is the post-refactor OKX project per ADR-001.

---

## Checklist Results

### 1. Layer Chain — Bitget references Core + Http ONLY

Verified via `dotnet list src/CryptoExchanges.Net.Bitget reference`:
- `../CryptoExchanges.Net.Core/CryptoExchanges.Net.Core.csproj` — CORRECT
- `../CryptoExchanges.Net.Http/CryptoExchanges.Net.Http.csproj` — CORRECT
No Binance, OKX, Bybit, or DI package references. Dependency direction: PASS.

### 2. InternalsVisibleTo — No IVT to DI package (ADR-001 post-refactor posture)

csproj contains:
- `CryptoExchanges.Net.Bitget.Tests.Unit` — CORRECT
- `CryptoExchanges.Net.Bitget.Tests.Integration` — CORRECT
- `DynamicProxyGenAssembly2` — CORRECT (NSubstitute/Castle DynamicProxy)
No IVT to `CryptoExchanges.Net.DependencyInjection`. Matches OKX (the post-ADR-001 template, not the older Binance which only has integration tests in its IVT). PASS.

### 3. ExchangeId.Bitget — No Core edit in this task

`ExchangeId.Bitget` is confirmed present in Core (added by TASK-016).
`git diff 3ca50b8..9029eab -- src/CryptoExchanges.Net.Core/` produces no output — no Core modifications in this task's commit. PASS.

### 4. NoWarn, PackageReferences consistent with OKX template

NoWarn: `$(NoWarn);CA1812;CA1056;CA1031;CA2000;CA1305;CA1032;CS1591` — byte-for-byte identical to OKX template. PASS.
Packages match OKX exactly: DeltaMapper 1.2.0, ME.Extensions.DI.Abstractions 10.0.*, ME.Extensions.Http 10.0.*, ME.Extensions.Options 10.0.*. PASS.

### 5. Solution registration

Bitget project GUID `975387A7-B48B-4FD2-96F0-238BA8580CBC` registered under the `src` solution folder (`827E0CD3...`). Full Debug/Release x Any CPU/x64/x86 config rows added (12 rows, matching OKX pattern). PASS.

### 6. GlobalUsings.cs

Identical to OKX GlobalUsings: `System.Text.Json`, `System.Text.Json.Serialization`, Core.Enums, Core.Interfaces, Core.Models. PASS.

### 7. BitgetOptions shape (Acceptance Criteria 2)

All required members present: `BaseUrl`, `ApiKey`, `SecretKey`, `Passphrase`, `TimeoutSeconds`, `ToCredentials()`. All have XML `<summary>` tags. `public sealed` class in the correct namespace. PASS.

### 8. Build gate

`dotnet build src/CryptoExchanges.Net.Bitget/CryptoExchanges.Net.Bitget.csproj` → Build succeeded. 0 Warning(s), 0 Error(s). PASS.
`dotnet build CryptoExchanges.Net.sln` → Build succeeded. 0 Warning(s), 0 Error(s). PASS.

---

## Findings

### Finding: ToCredentials() passes string.Empty as passphrase, which will throw at runtime for unauthenticated callers
- **Severity**: LOW
- **Confidence**: 72
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetOptions.cs:24-25`
- **Category**: Architecture (contract correctness)
- **Verdict**: CONCERN (non-blocking — confidence 72 < 80)
- **Issue**: `Passphrase` defaults to `string.Empty`. `ExchangeCredentials(string, string, string?)` accepts the passphrase as `string?`. When passed a non-null empty string (which `string.Empty` is), the constructor's guard `ArgumentException.ThrowIfNullOrWhiteSpace(passphrase)` will throw if the caller invokes `ToCredentials()` without setting a passphrase first. A consumer who uses only public (unauthenticated) endpoints may never call `ToCredentials()` so the runtime impact may be nil, but the contract is confusing: the property default suggests "not set yet" while the constructor rejects that empty-string sentinel. OkxOptions has the identical behaviour — this is an inherited OKX smell, not a Bitget-specific defect.
- **Fix**: Pass `passphrase: string.IsNullOrEmpty(Passphrase) ? null : Passphrase` so callers who do not need a passphrase (e.g. public-market-only mode) get a valid no-passphrase credential rather than an exception. This is a pre-existing pattern-level smell (also present in OkxOptions) — flag for backlog, not a blocking change here.
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Auth/ExchangeCredentials.cs:40-49` (constructor guards)

### Finding: Bitget.Tests.Unit IVT added before the test project exists
- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Bitget/CryptoExchanges.Net.Bitget.csproj:14`
- **Category**: Architecture (forward declaration)
- **Verdict**: CONCERN (non-blocking — confidence 55 < 80)
- **Issue**: `InternalsVisibleTo Include="CryptoExchanges.Net.Bitget.Tests.Unit"` is declared here but the Tests.Unit project does not yet exist (tests arrive in TASK-022). This is intentional scaffolding (mirrors OKX template exactly at initial setup) and causes no build failure. However it is a dead declaration until TASK-022 lands.
- **Fix**: No immediate action required; verify at TASK-022 that the project name matches exactly.
- **Pattern reference**: OKX csproj (identical pre-declaration pattern)

---

## Summary

- PASS: Layer chain — Core + Http ProjectReferences only, confirmed by `dotnet list` and grep.
- PASS: InternalsVisibleTo — Tests.Unit, Tests.Integration, DynamicProxyGenAssembly2 only; no DI package IVT (ADR-001 compliant).
- PASS: No Core edit in TASK-017 commit; ExchangeId.Bitget already present from TASK-016.
- PASS: NoWarn set, PackageReferences, DeltaMapper version identical to OKX post-refactor template.
- PASS: Solution registration — correct GUID, correct src folder nesting, complete config platform rows.
- PASS: GlobalUsings.cs mirrors OKX exactly.
- PASS: BitgetOptions — all required public properties and ToCredentials() present, all XML-documented.
- PASS: Build gate — 0 warnings, 0 errors on both Bitget project and full solution.
- CONCERN: ToCredentials() passes `string.Empty` passphrase as non-null, which ExchangeCredentials constructor will reject — pre-existing OKX smell, inherited by faithful cloning; confidence 72, non-blocking.
- CONCERN: Tests.Unit IVT is a forward declaration with no matching project yet; harmless but inert until TASK-022; confidence 55, non-blocking.

---

## Final Verdict

**APPROVED**

All 11 invariant checks pass. Both concerns are non-blocking (confidence below threshold and/or pre-existing pattern-level issues). The scaffold faithfully mirrors the post-ADR-001 OKX template with no dependency direction violations, no interface additions, no captive-dependency risk, and no Core mutations.

### Milestone architecture note (M-BITGET opening)

The `ToCredentials()` / empty-passphrase footgun now exists in two exchange projects (OKX, Bitget) and will appear in every future exchange that carries a passphrase. The fix is a one-liner change in both `OkxOptions` and `BitgetOptions` (null-coalescing empty to null before constructing `ExchangeCredentials`). Recommend scheduling a single backlog task to fix both before the public NuGet release; the longer it compounds across exchanges the more confusing the behaviour for SDK consumers who attempt to use public-only mode without supplying credentials.
