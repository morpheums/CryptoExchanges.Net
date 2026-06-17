---
id: TASK-017
status: PLANNED
---

# TASK-017: Bitget project scaffold + passphrase options + DI seam stub

**Milestone**: M-BITGET
**Wave**: 12
**Group**: 12
**Status**: PLANNED
**Depends on**: TASK-016
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#bitget (priority 3; passphrase credential; slots into OKX-era abstraction)
**Blast radius**: MEDIUM — new src project + sln entry; uses `ExchangeId.Bitget` from TASK-016.

## Description
Create the `CryptoExchanges.Net.Bitget` class library (Core + Http refs, GlobalUsings, csproj matching Binance posture), add it to the solution, and define public `BitgetOptions` mirroring `OkxOptions` (ApiKey/SecretKey/Passphrase/BaseUrl/TimeoutSeconds). No signing yet.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Bitget/CryptoExchanges.Net.Bitget.csproj`
- `src/CryptoExchanges.Net.Bitget/GlobalUsings.cs`
- `src/CryptoExchanges.Net.Bitget/BitgetOptions.cs`
### Modifies
- `CryptoExchanges.Net.sln`

## Files modified
- src/CryptoExchanges.Net.Bitget/CryptoExchanges.Net.Bitget.csproj
- src/CryptoExchanges.Net.Bitget/GlobalUsings.cs
- src/CryptoExchanges.Net.Bitget/BitgetOptions.cs
- CryptoExchanges.Net.sln

## Pattern Reference
- `src/CryptoExchanges.Net.Binance/CryptoExchanges.Net.Binance.csproj` (project refs + NoWarn)
- `src/CryptoExchanges.Net.Okx/OkxOptions.cs` (passphrase-carrying options shape from TASK-010)

## Acceptance Criteria
1. `dotnet build CryptoExchanges.Net.sln` succeeds; Bitget project references Core + Http only.
2. `BitgetOptions` is public with a `Passphrase` field plus ApiKey/SecretKey/BaseUrl/TimeoutSeconds; all members XML-documented.
3. Uses `ExchangeId.Bitget`; no Core edit in this task; no reference to other exchange projects.

## Test Requirements
- No tests this task (scaffolding); build green. Tests arrive in TASK-022.
