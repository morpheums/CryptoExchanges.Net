---
reviewer: api-reviewer
task: TASK-056
verdict: APPROVE
confidence: 97
---

# API Review — TASK-056

## Summary
TASK-056 scaffolds the `CryptoExchanges.Net.Kucoin` package with `KucoinOptions`, `KucoinSymbolFormat`, `GlobalUsings`, the csproj, and two test projects. The public API surface is fully consistent with the OKX pattern. No Core interfaces were touched; no breaking changes were introduced.

## Findings

### KucoinOptions public surface matches OKX pattern exactly (SEVERITY: LOW | confidence: 99%)
`KucoinOptions` is `sealed class` with `BaseUrl`, `ApiKey`, `SecretKey`, `Passphrase`, `TimeoutSeconds`, and `ToCredentials()` — identical shape to `OkxOptions`. All five properties carry XML `<summary>` docs. `ToCredentials()` has full `<summary>`, `<returns>`, and `<exception>` docs. The `Passphrase` doc additionally explains KuCoin's KC-API-KEY-VERSION 2 signing requirement, which is accurate and useful for consumers. No issues.

### KucoinSymbolFormat is correctly internal (SEVERITY: LOW | confidence: 99%)
`KucoinSymbolFormat` is `internal static`, matching `OkxSymbolFormat` exactly. The `Instance` field is `public static readonly SymbolFormat`, which is the right visibility for test accessibility via `InternalsVisibleTo`. Hyphen delimiter and `SymbolCasing.Upper` are correct for KuCoin's wire format (e.g. `BTC-USDT`). `FallbackQuoteAssets` list is identical to OKX's, which is a safe default for a scaffold — the correct exchange-specific ordering can be refined in the mapper task.

### NuGet package metadata is complete and correct (SEVERITY: LOW | confidence: 99%)
`PackageId`, `Description`, `RootNamespace`, and `AssemblyName` are all set. `PackageLicenseExpression`, `Authors`, `Version`, `PackageReadmeFile`, and `PackageIcon` are inherited from `Directory.Build.props`. `GenerateDocumentationFile` is inherited (`true` from `Directory.Build.props:8`). The csproj matches `CryptoExchanges.Net.Okx.csproj` line-for-line except for exchange-specific names.

### InternalsVisibleTo list is appropriately scoped (SEVERITY: LOW | confidence: 99%)
Three entries: `CryptoExchanges.Net.Kucoin.Tests.Unit`, `CryptoExchanges.Net.Kucoin.Tests.Integration`, and `DynamicProxyGenAssembly2` (for NSubstitute/Castle DynamicProxy). This mirrors the OKX pattern exactly. No consumer application project is granted visibility.

### Test projects have IsPackable=false and IsTestProject=true (SEVERITY: LOW | confidence: 99%)
Both `.Tests.Unit` and `.Tests.Integration` csproj files set `<IsPackable>false</IsPackable>` and `<IsTestProject>true</IsTestProject>`. This matches the library convention.

### ToCredentials() passes empty Passphrase to ExchangeCredentials constructor (SEVERITY: LOW | confidence: 85%)
When `KucoinOptions.Passphrase` is `string.Empty` (the default), `ToCredentials()` passes it as a non-null `string` to `ExchangeCredentials(apiKey, secretKey, passphrase)`. The constructor's guard `ArgumentException.ThrowIfNullOrWhiteSpace(passphrase)` fires for a non-null empty passphrase, throwing at runtime. This is identical behavior to `OkxOptions.ToCredentials()` and is therefore a pre-existing pattern — not a regression introduced by this task. The XML doc on `ToCredentials()` correctly documents this with `<exception cref="ArgumentException">`. Non-blocking.

### LR-004: No array parameter index access in this task (SEVERITY: N/A)
`KucoinOptions` and `KucoinSymbolFormat` contain no methods with array parameters accessed by index. LR-004 does not apply.

## Verdict: APPROVE
All public API surface is structurally identical to the OKX scaffold (the designated pattern for this task). `KucoinOptions` is correctly documented, `KucoinSymbolFormat` is correctly scoped as `internal`, NuGet metadata is complete, `InternalsVisibleTo` is appropriately limited, and both test projects are marked non-packable. The one noted behavior (empty-passphrase `ToCredentials()` throws) is a pre-existing OKX-pattern characteristic, documented in the XML docs, and out of scope for this scaffold task.
