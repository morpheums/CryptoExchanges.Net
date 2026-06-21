---
verdict: APPROVED
task: TASK-065 through TASK-070 (consolidated FEAT-007 review)
reviewer: api-reviewer
date: 2026-06-21
---

# API Review — FEAT-007 Consolidated (TASK-065..070)

> This is a consolidated FEAT-007 rename review. The same evidence applies to TASK-065..070.

## Summary

The FEAT-007 refactor is a clean mechanical rename of the aggregator package from `CryptoExchanges.Net.DependencyInjection` to `CryptoExchanges.Net`. The public API surface — method signatures, options shape, per-exchange DI extensions — is byte-identical to the prior release. The intentional breaking change (package id and namespace) is accurately documented and properly versioned.

## Checklist

### 1. Method signature byte-stable

Verified in `src/CryptoExchanges.Net/ServiceCollectionExtensions.cs:25-27`:

```csharp
public static IServiceCollection AddCryptoExchanges(
    this IServiceCollection services,
    Action<CryptoExchangesOptions>? configure = null)
```

Signature is identical to the prior contract: same method name, same parameter names and types, same optional default on `configure`. PASS.

### 2. Options shape byte-stable

Verified in `src/CryptoExchanges.Net/CryptoExchangesOptions.cs`. All expected properties present and typed correctly:

- `BinanceBaseUrl`, `BinanceApiKey`, `BinanceSecretKey`
- `BybitBaseUrl`, `BybitApiKey`, `BybitSecretKey`
- `OkxBaseUrl`, `OkxApiKey`, `OkxSecretKey`, `OkxPassphrase`
- `BitgetBaseUrl`, `BitgetApiKey`, `BitgetSecretKey`, `BitgetPassphrase`
- `KucoinBaseUrl`, `KucoinApiKey`, `KucoinSecretKey`, `KucoinPassphrase`

All are `string?` nullable setters. No properties removed, no types changed, no new required properties added. PASS.

### 3. CHANGELOG accuracy

Verified `CHANGELOG.md` section `[0.5.0-preview.1] — 2026-06-21`:

- Correctly labels the change as `[BREAKING — package id / namespace]`.
- Names old package (`CryptoExchanges.Net.DependencyInjection`) and new package (`CryptoExchanges.Net`) explicitly.
- States that `AddCryptoExchanges` and `CryptoExchangesOptions` moved to namespace `CryptoExchanges.Net`.
- States explicitly: "Method name and options shape are unchanged."
- Migration block covers all three required steps: remove old package, add new package, update `using` directive.
- States "`services.AddCryptoExchanges(...)` — unchanged."

PASS.

### 4. Version consistency

`Directory.Build.props:20` sets `<Version>0.5.0-preview.1</Version>`. CHANGELOG header is `[0.5.0-preview.1] — 2026-06-21`. These match. PASS.

### 5. NuGet badge accuracy

`README.md:9`: badge points to `CryptoExchanges.Net` package (`https://www.nuget.org/packages/CryptoExchanges.Net`). `NUGET_README.md:7`: badge also points to `CryptoExchanges.Net`. Neither references the old `CryptoExchanges.Net.DependencyInjection` package. PASS.

### 6. 9-package set correct

`dotnet sln list` (filtered to non-test projects) yields:

```
src/CryptoExchanges.Net/CryptoExchanges.Net.csproj          ← new aggregator (replaces DI package)
src/CryptoExchanges.Net.Binance/...
src/CryptoExchanges.Net.Bitget/...
src/CryptoExchanges.Net.Bybit/...
src/CryptoExchanges.Net.Core/...
src/CryptoExchanges.Net.Http/...
src/CryptoExchanges.Net.Kucoin/...
src/CryptoExchanges.Net.Mcp/...
src/CryptoExchanges.Net.Okx/...
```

Nine packable projects. `CryptoExchanges.Net.DependencyInjection` is absent. `CryptoExchanges.Net` is present. PASS.

### 7. Per-exchange `using` unchanged

`ServiceCollectionExtensions.cs:1-5` uses `using CryptoExchanges.Net.Binance;` etc. for internal delegation only. Consumers calling `AddBinanceExchange` directly still only need `using CryptoExchanges.Net.Binance;` — that namespace is unchanged. PASS.

### 8. NuGet project metadata

`src/CryptoExchanges.Net/CryptoExchanges.Net.csproj`:

- `<PackageId>CryptoExchanges.Net</PackageId>` — correct.
- `<Description>` present and descriptive.
- `<PackageLicenseExpression>Apache-2.0</PackageLicenseExpression>` inherited from `Directory.Build.props:13` — correct.
- `<GenerateDocumentationFile>true</GenerateDocumentationFile>` inherited from `Directory.Build.props:8` — correct.
- No `<IsPackable>false</IsPackable>` — correct; this is a packable library project.

PASS.

### 9. No new InternalsVisibleTo

No `InternalsVisibleTo` introduced in source projects. PASS.

### 10. Core interfaces unmodified

This refactor touches only the aggregator DI package. No changes to `src/CryptoExchanges.Net.Core/Interfaces/` or `src/CryptoExchanges.Net.Core/Models/`. No interface members added/removed, no record constructors changed, no enum members reordered. PASS.

## Findings

No blocking findings. No concerns.

The NUGET_README.md packages table (`CryptoExchanges.Net.Binance · .Bybit · .Okx · .Bitget`) does not list KuCoin, but this is a pre-existing content gap unrelated to the FEAT-007 rename scope and carries no API surface consequence.

## Verdict

APPROVED
