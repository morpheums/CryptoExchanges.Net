---
id: TASK-010
status: PLANNED
---

# TASK-010: OKX project scaffold + passphrase options + DI seam stub

**Milestone**: M-OKX
**Wave**: 7
**Group**: 7
**Status**: PLANNED
**Depends on**: TASK-009
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#okx (priority 2; 3rd credential = passphrase; reuses existing `ExchangeId.Okx`)
**Blast radius**: MEDIUM — new src project + sln entry; reuses `ExchangeId.Okx` (no Core enum change).

## Description
Create the `CryptoExchanges.Net.Okx` class library (Core + Http refs, GlobalUsings, csproj matching Binance posture), add it to the solution, and define public `OkxOptions` mirroring `BybitOptions` PLUS a `Passphrase` field (the 3rd OKX credential). Optionally surface an `ExchangeCredentials` accessor built from the options. No signing yet.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Okx/CryptoExchanges.Net.Okx.csproj`
- `src/CryptoExchanges.Net.Okx/GlobalUsings.cs`
- `src/CryptoExchanges.Net.Okx/OkxOptions.cs`
### Modifies
- `CryptoExchanges.Net.sln`

## Files modified
- src/CryptoExchanges.Net.Okx/CryptoExchanges.Net.Okx.csproj
- src/CryptoExchanges.Net.Okx/GlobalUsings.cs
- src/CryptoExchanges.Net.Okx/OkxOptions.cs
- CryptoExchanges.Net.sln

## Pattern Reference
- `src/CryptoExchanges.Net.Binance/CryptoExchanges.Net.Binance.csproj` (project refs + NoWarn)
- `BinanceOptions` shape (`BinanceExchangeClient.cs:14-28`) + new `ExchangeCredentials` from TASK-009 for the passphrase field

## Acceptance Criteria
1. `dotnet build CryptoExchanges.Net.sln` succeeds; Okx project references Core + Http only (layer chain preserved).
2. `OkxOptions` is public with a required-when-signed `Passphrase` field plus ApiKey/SecretKey/BaseUrl/TimeoutSeconds; all members XML-documented (CS1591 clean).
3. Uses existing `ExchangeId.Okx`; no Core enum edit; no reference to Binance/Bybit projects.

## Test Requirements
- No tests this task (scaffolding); build green. Tests arrive in TASK-015.
