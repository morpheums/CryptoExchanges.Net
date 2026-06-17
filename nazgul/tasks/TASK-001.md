---
id: TASK-001
status: READY
---

# TASK-001: Bybit project scaffold + options + DI seam stub

**Milestone**: M-BYBIT
**Wave**: 1
**Group**: 1
**Status**: READY
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
