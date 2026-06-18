---
id: TASK-010
status: DONE
---

# TASK-010: OKX project scaffold + passphrase options + DI seam stub

**Milestone**: M-OKX
**Wave**: 7
**Group**: 7
**Status**: IMPLEMENTED
**Base SHA**: 1e5300b137deab86b24561c8f829e71a665ec150
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

## Implementation Notes

Pure structural scaffolding for the `CryptoExchanges.Net.Okx` class library, mirroring the
current post-ADR-001 (TASK-009B) Bybit project — NOT Binance. No signing/services/DI yet.

### Files created
- `src/CryptoExchanges.Net.Okx/CryptoExchanges.Net.Okx.csproj`
- `src/CryptoExchanges.Net.Okx/GlobalUsings.cs`
- `src/CryptoExchanges.Net.Okx/OkxOptions.cs`
- Modified `CryptoExchanges.Net.sln` (added the project + full config entries + nesting).

### OkxOptions field decisions
`public sealed class OkxOptions` in namespace `CryptoExchanges.Net.Okx`, mirroring `BybitOptions`:
- `BaseUrl` — default `https://www.okx.com` (OKX REST base; Bybit used `api.bybit.com`).
- `ApiKey`, `SecretKey` — same shape as Bybit (empty-string defaults).
- `Passphrase` (string, empty default) — **the 3rd OKX credential**. OKX requires it for all
  signed/private endpoints (it is the passphrase set when the API key is created). XML doc states
  it is required for signed/private endpoints and may be left empty for public market data.
- `TimeoutSeconds` — default 30 (same as Bybit).
- **NO `ReceiveWindow`** — deliberately omitted. Bybit's `ReceiveWindow` maps to its `recv-window`
  header convention. OKX does NOT use a recv-window; it authenticates with an ISO-8601 UTC timestamp
  baked into the signature prehash. Adding a Bybit-style recv-window here would be dead/misleading
  config. (No signing fields added in this scaffold — the timestamp/prehash logic lands in TASK-011.)
- Convenience `ToCredentials()` → Core `ExchangeCredentials(ApiKey, SecretKey, Passphrase)`,
  forwarding the passphrase. Kept because it is clean (the Core credential from TASK-009 is
  passphrase-capable and validates inputs itself). Pulls in `using CryptoExchanges.Net.Core.Auth;`
  at file scope (Auth is intentionally NOT a global using — mirrors Bybit's GlobalUsings, which
  omit Auth/Services).
- Every member is XML-documented; build is CS1591-clean under the inherited
  `TreatWarningsAsErrors` posture (CS1591 is also in NoWarn, matching Bybit).

### csproj / IVT posture (ADR-001)
- ProjectReferences: **Core + Http only** — preserves the layer chain; NO Binance/Bybit/DI refs.
  Confirmed via `dotnet list src/CryptoExchanges.Net.Okx reference`.
- PackageReferences mirror Bybit exactly: `DeltaMapper 1.2.0`,
  `Microsoft.Extensions.DependencyInjection.Abstractions 10.0.*`, `Microsoft.Extensions.Http 10.0.*`,
  `Microsoft.Extensions.Options 10.0.*`. (OKX will host its own `AddOkxExchange` in-assembly per
  ADR-001, like Bybit now does — hence the DI.Abstractions ref.)
- `NoWarn` list identical to Bybit: `CA1812;CA1056;CA1031;CA2000;CA1305;CA1032;CS1591`.
- `InternalsVisibleTo`: mirrors Bybit's CURRENT declarations exactly —
  `CryptoExchanges.Net.Okx.Tests.Unit`, `CryptoExchanges.Net.Okx.Tests.Integration` (future OKX test
  projects, arriving in TASK-015), and `DynamicProxyGenAssembly2` (so NSubstitute/Castle can mock the
  future internal `IOkxHttpClient`). **No IVT for the DependencyInjection package** (ADR-001).

### GlobalUsings
Identical set to Bybit's: `System.Text.Json` + `Serialization`, `Core.Enums`, `Core.Interfaces`,
`Core.Models`. Services/Auth usings omitted (those types don't exist yet for OKX; Auth is referenced
explicitly in OkxOptions.cs only).

### ExchangeId
Reuses existing `ExchangeId.Okx` (Core/Enums/Enums.cs:134). NO Core enum edit.

### sln registration
Added via `dotnet sln add --solution-folder src`. Project GUID `{179D89FC-030B-48CD-9893-D33E87FF1135}`
(CLI-generated; GUIDs only need uniqueness). Full Debug/Release × Any CPU/x64/x86 config entries
present (all platforms map to `Any CPU` like every sibling project), and nested under the `src`
solution folder `{827E0CD3-B72D-47B6-A68D-7590B98EB39B}`.

### Verification output
- `dotnet build CryptoExchanges.Net.sln` → **Build succeeded. 0 Warning(s), 0 Error(s).**
- `dotnet list src/CryptoExchanges.Net.Okx reference` → Core + Http only (confirmed).
- `dotnet test --filter 'Category!=Integration'` → all green (Bybit 80, DI 11, Http 12, Core/Binance
  units + Binance integration 45 — 0 failures across the suite).

### Deviations
None. Followed the Bybit (post-refactor) template exactly. The one judgment call — including the
optional `ToCredentials()` convenience — was explicitly permitted by the task ("only if clean") and
is clean (no signing logic, just forwards the passphrase to the validated Core credential).

## Commits
- af642795acfca20118a0f73c8a87c0de7c615b73 — feat(M2): TASK-010 OKX project scaffold + passphrase options + DI seam stub

## Review
- **Review Gate**: PASSED (round 1) — all 4 APPROVE: architect 96, code (high), security 92, api 96. No blocking items.
- **Verified**: Core+Http-only refs; csproj mirrors post-ADR-001 Bybit (no IVT to DI package); reuses ExchangeId.Okx; OkxOptions surface correct (no ReceiveWindow); CS1591 clean.
- **CARRIED to TASK-011 (non-blocking CONCERN, 3 reviewers @75/82/72)**: `ToCredentials()` throws on empty/whitespace Passphrase (ExchangeCredentials treats null as the no-passphrase sentinel, rejects empty). DECISION DEFERRED to TASK-011 signing wire-up: the OKX secret-gated finalizer must gate on secret AND passphrase (→ PassThrough when either absent) so `ToCredentials()` is only called when both present; OR map empty→null in ToCredentials. Pick during finalizer design + tighten the `<exception>` doc then. Also: OkxOptions has no ToString redaction (security @45, matches BybitOptions — convention lives in ExchangeCredentials); PackageTags missing 'okx' (api @70, pre-existing, fold into cleanup).
