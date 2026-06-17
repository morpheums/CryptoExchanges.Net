# API Review: TASK-001

**Verdict**: APPROVE
**Reviewer**: api-reviewer
**Confidence**: 97

## Summary
TASK-001 is a pure scaffolding diff — three new files and a solution entry. `BybitOptions` is a property-for-property mirror of `BinanceOptions`, the csproj mirrors the Binance csproj in every material dimension, and no existing public API in Core, Http, Binance, or DI was touched. No blocking issues found.

## Findings

No findings. All checks pass without exception.

Additional observations (informational, non-blocking):

1. **GlobalUsings omits Services/Auth namespaces** — INFORMATIONAL (confidence: 100)
   `src/CryptoExchanges.Net.Bybit/GlobalUsings.cs` does not include `CryptoExchanges.Net.Bybit.Services` or `CryptoExchanges.Net.Bybit.Auth`. This is intentional and correctly documented in the task notes: those namespaces do not exist yet. The Binance GlobalUsings includes them only because those namespaces exist in the Binance project. No action needed; downstream tasks (TASK-002 onward) will add them.

2. **InternalsVisibleTo for non-existent test project** — INFORMATIONAL (confidence: 100)
   `CryptoExchanges.Net.Bybit.Tests.Integration` is referenced in `InternalsVisibleTo` before the test project exists (arrives in TASK-008). This is forward-planning and carries zero risk — `InternalsVisibleTo` has no effect until an assembly with that exact name is compiled. The naming convention `CryptoExchanges.Net.Bybit.Tests.Integration` is consistent with how the Binance integration test assembly name is declared (`CryptoExchanges.Net.Binance.Tests.Integration` in `tests/CryptoExchanges.Net.Binance.Tests.Integration/CryptoExchanges.Net.IntegrationTests.csproj:AssemblyName`).

## API Surface Checklist

- **BybitOptions mirrors BinanceOptions shape**: PASS
  - `BaseUrl` (string, default `"https://api.bybit.com"`) — matches pattern, Bybit-appropriate URL
  - `ApiKey` (string, default `string.Empty`) — exact match
  - `SecretKey` (string, default `string.Empty`) — exact match
  - `TimeoutSeconds` (int, default 30) — exact match
  - `ReceiveWindow` (decimal, default 5000m) — exact match
  - XML doc on every member — PASS (CS1591 compliance confirmed, build 0 warnings)

- **No breaking changes to existing APIs**: PASS
  No files in Core, Http, Binance, or DI were modified. The diff touches only new files under `src/CryptoExchanges.Net.Bybit/`, the solution file (additive), and Nazgul metadata files (plan.md, task manifest).

- **csproj NuGet metadata present**: PASS
  - `PackageId`: `CryptoExchanges.Net.Bybit` — present
  - `Description`: `Bybit exchange implementation for CryptoExchanges.Net.` — present
  - `RootNamespace` and `AssemblyName`: present, correct
  - `Authors`, `PackageLicenseExpression` (Apache-2.0), `Version` (0.1.0-preview.1), `PackageReadmeFile`: all inherited from `Directory.Build.props` — consistent with Binance csproj which also does not redeclare them
  - `NoWarn` list mirrors Binance exactly: `CA1812;CA1056;CA1031;CA2000;CA1305;CA1032;CS1591`

- **TargetFramework net10.0**: PASS
  Not declared locally (same as Binance csproj); inherited from `Directory.Build.props:3` which sets `<TargetFramework>net10.0</TargetFramework>`.

- **Namespace conventions**: PASS
  Root namespace `CryptoExchanges.Net.Bybit`, type `BybitOptions` in file `BybitOptions.cs` — follows the `BinanceOptions` convention exactly.

- **No unintended public types**: PASS
  The only new public type is `BybitOptions`, which is the intended public surface for this task. No other types are declared in the diff.
