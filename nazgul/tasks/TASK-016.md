---
id: TASK-016
status: IN_PROGRESS
---

# TASK-016: Core ExchangeId.Bitget enum member

**Milestone**: M-BITGET
**Wave**: 11
**Group**: 11
**Status**: PLANNED
**Depends on**: TASK-015
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#bitget (priority 3; needs a new ExchangeId member — Binance/Bybit/Okx already exist, Bitget does not)
**Blast radius**: MEDIUM — touches the Core public enum (api-reviewer + architect-reviewer); additive enum member only.

## Description
Add a `Bitget` member to the Core `ExchangeId` enum. This is the only Core change required for Bitget (Bybit and Okx members already exist). Append the member to preserve existing numeric ordinals (additive, non-breaking) and add the XML doc comment matching the existing members' style.

## File Scope
### Creates
- (none)
### Modifies
- `src/CryptoExchanges.Net.Core/Enums/Enums.cs`

## Files modified
- src/CryptoExchanges.Net.Core/Enums/Enums.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Core/Enums/Enums.cs:123-135` (existing `ExchangeId` members with `/// <summary>` docs; Binance, Coinbase, Bybit, Kraken, Okx, KuCoin)

## Acceptance Criteria
1. `ExchangeId.Bitget` exists with an XML `<summary>` doc; it is appended so existing members keep their ordinals (no reordering).
2. `dotnet build CryptoExchanges.Net.sln` is clean under TreatWarningsAsErrors (CS1591 satisfied).
3. No other Core type is modified; api-reviewer confirms the change is additive (non-breaking) to the public enum.

## Test Requirements
- A Core unit test asserts `ExchangeId.Bitget` is defined; no behavioral test needed for an enum member.
