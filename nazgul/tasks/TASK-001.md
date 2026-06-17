---
id: TASK-001
status: IMPLEMENTED
---

# TASK-001: Bybit project scaffold + options + DI seam stub

**Milestone**: M-BYBIT
**Wave**: 1
**Group**: 1
**Status**: IMPLEMENTED
**Depends on**: none
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#bybit (Bybit = priority 1, HMAC-SHA256 hex, lowest effort)
**Blast radius**: MEDIUM — adds a new src project + sln entry; reuses existing `ExchangeId.Bybit` (no Core change).

## Description
Create the `CryptoExchanges.Net.Bybit` class library project mirroring the Binance project shape (Core + Http project references, `GlobalUsings.cs`, csproj with same `NoWarn`/analyzer posture). Add it to the solution. Define the public `BybitOptions` type (BaseUrl, ApiKey, SecretKey, TimeoutSeconds, ReceiveWindow) mirroring `BinanceOptions`. No signing logic yet — this is structural scaffolding so downstream tasks have a compiling home.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj`
- `src/CryptoExchanges.Net.Bybit/GlobalUsings.cs`
- `src/CryptoExchanges.Net.Bybit/BybitOptions.cs`
### Modifies
- `CryptoExchanges.Net.sln`

## Files modified
- src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj
- src/CryptoExchanges.Net.Bybit/GlobalUsings.cs
- src/CryptoExchanges.Net.Bybit/BybitOptions.cs
- CryptoExchanges.Net.sln

## Pattern Reference
- `src/CryptoExchanges.Net.Binance/CryptoExchanges.Net.Binance.csproj` (project refs to Core + Http, DeltaMapper + ME.Http/Options packages, NoWarn list)
- `src/CryptoExchanges.Net.Binance/GlobalUsings.cs`
- `BinanceOptions` (defined in `src/CryptoExchanges.Net.Binance/BinanceExchangeClient.cs:14-28`) for options shape

## Acceptance Criteria
1. `dotnet build CryptoExchanges.Net.sln` succeeds with the new project referenced (Core + Http only — preserves layer chain).
2. `BybitOptions` is public, has XML docs on every member (CS1591 clean under TreatWarningsAsErrors), and mirrors `BinanceOptions` fields.
3. No reference from Bybit to DI or to any other exchange project; `ExchangeId.Bybit` (already in Core) is used — no Core enum edit.

## Test Requirements
- No new tests in this task (pure scaffolding); build must be green. Tests arrive in TASK-008.

## Implementation Notes

### What was created
- **src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj** — mirrors Binance csproj exactly: Core + Http project references, DeltaMapper 1.2.0, Microsoft.Extensions.Http/Options 10.0.*, same NoWarn list (CA1812;CA1056;CA1031;CA2000;CA1305;CA1032;CS1591), InternalsVisibleTo for Bybit.Tests.Integration and DependencyInjection.
- **src/CryptoExchanges.Net.Bybit/GlobalUsings.cs** — mirrors Binance GlobalUsings: System.Text.Json, System.Text.Json.Serialization, Core.Enums, Core.Interfaces, Core.Models. (Services and Auth usings are omitted since those namespaces don't exist yet in this scaffolding task.)
- **src/CryptoExchanges.Net.Bybit/BybitOptions.cs** — public sealed class with XML docs on every member; mirrors BinanceOptions fields: BaseUrl (default https://api.bybit.com), ApiKey, SecretKey, TimeoutSeconds (30), ReceiveWindow (5000m).

### What was modified
- **CryptoExchanges.Net.sln** — added Bybit project entry (GUID D5E6F7A8-B9C0-1234-ABCD-567890123456) and full Debug/Release|Any CPU/x64/x86 build configuration entries.

### Verification
-  confirmed pre-existing in  — no Core edit required.
- Layer chain preserved: Bybit references Core + Http only (no DI, no other exchange project).
-   Determining projects to restore...
  All projects are up-to-date for restore.
  CryptoExchanges.Net.Core -> /Users/josemejia/ai-platform/personal/workspace/repos/CryptoExchanges.Net/src/CryptoExchanges.Net.Core/bin/Debug/net10.0/CryptoExchanges.Net.Core.dll
  CryptoExchanges.Net.Http -> /Users/josemejia/ai-platform/personal/workspace/repos/CryptoExchanges.Net/src/CryptoExchanges.Net.Http/bin/Debug/net10.0/CryptoExchanges.Net.Http.dll
  CryptoExchanges.Net.Core.Tests -> /Users/josemejia/ai-platform/personal/workspace/repos/CryptoExchanges.Net/tests/CryptoExchanges.Net.Core.Tests.Unit/bin/Debug/net10.0/CryptoExchanges.Net.Core.Tests.Unit.dll
  CryptoExchanges.Net.Binance -> /Users/josemejia/ai-platform/personal/workspace/repos/CryptoExchanges.Net/src/CryptoExchanges.Net.Binance/bin/Debug/net10.0/CryptoExchanges.Net.Binance.dll
  CryptoExchanges.Net.Bybit -> /Users/josemejia/ai-platform/personal/workspace/repos/CryptoExchanges.Net/src/CryptoExchanges.Net.Bybit/bin/Debug/net10.0/CryptoExchanges.Net.Bybit.dll
  CryptoExchanges.Net.Http.Tests.Unit -> /Users/josemejia/ai-platform/personal/workspace/repos/CryptoExchanges.Net/tests/CryptoExchanges.Net.Http.Tests.Unit/bin/Debug/net10.0/CryptoExchanges.Net.Http.Tests.Unit.dll
  CryptoExchanges.Net.DependencyInjection -> /Users/josemejia/ai-platform/personal/workspace/repos/CryptoExchanges.Net/src/CryptoExchanges.Net.DependencyInjection/bin/Debug/net10.0/CryptoExchanges.Net.DependencyInjection.dll
  CryptoExchanges.Net.IntegrationTests -> /Users/josemejia/ai-platform/personal/workspace/repos/CryptoExchanges.Net/tests/CryptoExchanges.Net.Binance.Tests.Integration/bin/Debug/net10.0/CryptoExchanges.Net.Binance.Tests.Integration.dll
  BasicUsage -> /Users/josemejia/ai-platform/personal/workspace/repos/CryptoExchanges.Net/samples/BasicUsage/bin/Debug/net10.0/BasicUsage.dll
  CryptoExchanges.Net.DependencyInjection.Tests.Unit -> /Users/josemejia/ai-platform/personal/workspace/repos/CryptoExchanges.Net/tests/CryptoExchanges.Net.DependencyInjection.Tests.Unit/bin/Debug/net10.0/CryptoExchanges.Net.DependencyInjection.Tests.Unit.dll

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:02.67 — 0 warnings, 0 errors.

## Implementation Notes

### What was created
- **src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj** — mirrors Binance csproj: Core + Http project references, DeltaMapper 1.2.0, Microsoft.Extensions.Http/Options 10.0.*, same NoWarn list, InternalsVisibleTo for Bybit.Tests.Integration and DependencyInjection.
- **src/CryptoExchanges.Net.Bybit/GlobalUsings.cs** — mirrors Binance GlobalUsings: System.Text.Json, System.Text.Json.Serialization, Core.Enums, Core.Interfaces, Core.Models. Services/Auth usings omitted (those namespaces do not exist yet in this scaffold wave).
- **src/CryptoExchanges.Net.Bybit/BybitOptions.cs** — public sealed class with XML docs on every member; mirrors BinanceOptions fields: BaseUrl (default https://api.bybit.com), ApiKey, SecretKey, TimeoutSeconds (30), ReceiveWindow (5000m).

### What was modified
- **CryptoExchanges.Net.sln** — added Bybit project entry (GUID D5E6F7A8-B9C0-1234-ABCD-567890123456) and full Debug/Release configuration entries.

### Verification
- ExchangeId.Bybit confirmed pre-existing in Core/Enums/Enums.cs — no Core edit required.
- Layer chain preserved: Bybit references Core + Http only.
- dotnet build CryptoExchanges.Net.sln: 0 warnings, 0 errors.
