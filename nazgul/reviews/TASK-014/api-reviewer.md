# API Review — TASK-014: OkxHttpClient + IOkxHttpClient

**Reviewer**: API Reviewer
**Date**: 2026-06-18
**Branch**: feat/m3-okx
**Files reviewed**:
- `src/CryptoExchanges.Net.Okx/IOkxHttpClient.cs`
- `src/CryptoExchanges.Net.Okx/OkxHttpClient.cs`

**Pattern reference**: `src/CryptoExchanges.Net.Bybit/IBybitHttpClient.cs` and `BybitHttpClient.cs`

---

## Checklist Results

### 1. Signature parity with IBybitHttpClient

IOkxHttpClient declares GetAsync/PostAsync/DeleteAsync<T> with identical signatures:
- `(string endpoint, Dictionary<string,string>? parameters = null, bool signed = false, CancellationToken ct = default)` — GET
- `(string endpoint, Dictionary<string,string>? parameters = null, bool signed = true, CancellationToken ct = default)` — POST
- `(string endpoint, Dictionary<string,string>? parameters = null, bool signed = true, CancellationToken ct = default)` — DELETE

All parameter names, types, defaults, and ordering match IBybitHttpClient line-for-line. PASS.

### 2. Generic Task<T> return / CancellationToken last and optional

All three methods return `Task<T>`, `CancellationToken ct = default` is the final parameter on every member. PASS.

### 3. XML doc on interface and every member

Interface-level XML doc present (`IOkxHttpClient.cs:3`). All three method members have `<summary>` XML doc blocks. PASS.

### 4. internal visibility correct

`IOkxHttpClient` declared `internal` at `IOkxHttpClient.cs:4`. `OkxHttpClient` declared `internal sealed` at `OkxHttpClient.cs:29`. PASS.

### 5. IVT grants in csproj

`CryptoExchanges.Net.Okx.csproj` grants:
- `CryptoExchanges.Net.Okx.Tests.Unit` (line 19)
- `CryptoExchanges.Net.Okx.Tests.Integration` (line 20)
- `DynamicProxyGenAssembly2` (line 22) — NSubstitute Castle DynamicProxy

Matches the Bybit csproj pattern exactly. No consumer application assemblies granted. PASS.

### 6. Naming consistency (Okx* prefix)

Interface: `IOkxHttpClient`. Implementation: `OkxHttpClient`. Internal marker type: `OkxSigningRequest.MarkSigned`. All naming is Okx-prefixed and consistent with the Bybit precedent. PASS.

### 7. OkxHttpClient implementation body parity

OkxHttpClient.cs is a line-for-line structural mirror of BybitHttpClient.cs, substituting `OkxSigningRequest` for `BybitSigningRequest`. JsonOptions, BuildUrl, BuildQueryString, argument guards, ConfigureAwait(false), and null-forgiving `!` on deserialization are all present and identical. PASS.

### 8. NuGet package conventions

- `<PackageId>CryptoExchanges.Net.Okx</PackageId>` set (csproj:6).
- `<Description>OKX exchange implementation for CryptoExchanges.Net.</Description>` set (csproj:5).
- `<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>` is inherited from `Directory.Build.props:12` — consistent with the Bybit csproj which also relies on inheritance and does not repeat it locally.
- `<GenerateDocumentationFile>true</GenerateDocumentationFile>` inherited from `Directory.Build.props:8`. PASS.

### 9. PackageTags missing "okx"

`Directory.Build.props:16` defines `<PackageTags>` globally as `crypto;exchange;trading;sdk;binance;coinbase;bybit;bitcoin;ethereum`. The tag `okx` is absent. This is a minor NuGet discoverability gap. The Bybit csproj does not override tags either, so this is a pre-existing gap in the shared props rather than a regression introduced by this diff. However, as a new exchange being published, a per-package `<PackageTags>` override (or an update to the global set) would improve discoverability.

### 10. Extensibility: parameters typed as Dictionary<string,string>

The accepted Bybit pattern uses `Dictionary<string,string>?` for both query-string parameters and JSON-body parameters. OKX follows the same contract. No divergence. PASS.

---

## Findings

### Finding: PackageTags does not include "okx"
- **Severity**: LOW
- **Confidence**: 70
- **File**: `Directory.Build.props:16` / `src/CryptoExchanges.Net.Okx/CryptoExchanges.Net.Okx.csproj`
- **Category**: NuGet Conventions
- **Verdict**: CONCERN (non-blocking — confidence 70 < 80)
- **Issue**: The global `<PackageTags>` in `Directory.Build.props` lists `binance;bybit` but not `okx`. Consumers searching NuGet for "okx" will not find this package by tag. The Bybit csproj has the same gap, so this is a pre-existing build-props debt, not a regression from this diff.
- **Fix**: Either add a local `<PackageTags>` override in the OKX csproj (`<PackageTags>$(PackageTags);okx</PackageTags>`) or add `okx` to the global list in `Directory.Build.props`. The OKX-local override is less disruptive.
- **Pattern reference**: `Directory.Build.props:16`

---

## Summary

- PASS: IOkxHttpClient signature parity — exact member-for-member match with IBybitHttpClient (names, types, defaults, ordering).
- PASS: Async/cancellation convention — Task<T> return, CancellationToken last and optional on all three members.
- PASS: XML documentation — interface-level and per-member summaries present.
- PASS: internal visibility — both types correctly internal; sealed on the implementation.
- PASS: IVT grants — Unit + Integration test assemblies and DynamicProxyGenAssembly2 granted, no consumer apps.
- PASS: OkxHttpClient implementation body — structurally identical to BybitHttpClient, all guards and async patterns preserved.
- PASS: NuGet package metadata — PackageId, Description, License (inherited), GenerateDocumentationFile (inherited) all present.
- CONCERN: PackageTags missing "okx" — NuGet discoverability gap; pre-existing debt, non-blocking (confidence: 70/100).

---

## VERDICT: APPROVED, overall confidence 97

No blocking issues. The OkxHttpClient and IOkxHttpClient are a faithful structural mirror of the Bybit equivalents. All signature defaults, parameter names, visibility, IVT grants, XML docs, and implementation patterns are correct. The single CONCERN (missing "okx" NuGet tag) is pre-existing build-props debt and falls below the 80-confidence blocking threshold.
