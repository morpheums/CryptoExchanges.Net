---
id: TASK-017
status: DONE
---

> **Gate PASSED** (4/4: architect 95, code/security/api APPROVE). Commit 9029eab. Carry to TASK-018: gate signing on passphrase presence (don't call ToCredentials() on default options).

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

## Implementation Notes
- **Base SHA**: 3ca50b84cbb3d4ec018b524cb3a1dc4d00a6db7d
- Mirrored the post-refactor OKX src project (most recent template).
- Created `src/CryptoExchanges.Net.Bitget/`:
  - `CryptoExchanges.Net.Bitget.csproj` — Core + Http ProjectReferences ONLY; DeltaMapper 1.2.0 + Microsoft.Extensions.Http/Options/DependencyInjection.Abstractions (10.0.*); same `NoWarn` set as OKX; IVT for `CryptoExchanges.Net.Bitget.Tests.Unit`, `.Tests.Integration`, and `DynamicProxyGenAssembly2` (NSubstitute). No IVT for the DI package (ADR-001). No other exchange/DI refs.
  - `GlobalUsings.cs` — same global usings as OKX.
  - `BitgetOptions.cs` — `public sealed`, namespace `CryptoExchanges.Net.Bitget`: `BaseUrl` (default `https://api.bitget.com`), `ApiKey`, `SecretKey`, `Passphrase` (required for signed endpoints), `TimeoutSeconds` (30), plus `ToCredentials()` → Core `ExchangeCredentials(ApiKey, SecretKey, Passphrase)`. No ReceiveWindow. LEAN comments (ADR-001 conv 7) — one concise `<summary>` per public member, no `<remarks>`.
- Added `CryptoExchanges.Net.Bitget` to `CryptoExchanges.Net.sln` (GUID `975387A7-B48B-4FD2-96F0-238BA8580CBC`): full Debug/Release × Any CPU/x64/x86 config rows + nested under the `src` solution folder (`827E0CD3...`), mirroring the OKX registration.
- No Core edit; `ExchangeId.Bitget` (from TASK-016) available for later tasks.

## Verification
- `dotnet build CryptoExchanges.Net.sln` → **Build succeeded. 0 Warning(s), 0 Error(s).**
- `dotnet list src/CryptoExchanges.Net.Bitget reference` → Core + Http only (confirmed).

## Commits
- 9029eab feat(M4): TASK-017 Bitget project scaffold + passphrase options
