# Architect Review — TASK-020
## BitgetSymbolFormat + BitgetValueParsers + BitgetRequestValidation

**Reviewer**: Architect Reviewer
**Date**: 2026-06-18
**Branch**: feat/m4-bitget
**Files reviewed**:
- `src/CryptoExchanges.Net.Bitget/BitgetSymbolFormat.cs`
- `src/CryptoExchanges.Net.Bitget/Internal/BitgetValueParsers.cs`
- `src/CryptoExchanges.Net.Bitget/Internal/BitgetRequestValidation.cs`

---

## Checklist Pass

**Dependency direction (Invariant 1/2):** The Bitget `.csproj` references only Core and Http — no upward or lateral coupling to DI or another exchange. The three new files reference only Core types (`SymbolFormat`, `SymbolCasing`, `Asset`, `OrderSide`, `OrderType`, `OrderStatus`, `TimeInForce`) brought in via the project's `GlobalUsings.cs`. No Http-layer or sibling-exchange symbols are referenced. PASS.

**Core is untouched:** No changes to `src/CryptoExchanges.Net.Core/`. Core's `SymbolFormat`/`SymbolCasing` are consumed as-is. No new `ISymbolMapper` is introduced. PASS.

**Internal visibility:** All three classes are `internal static class`. The `InternalsVisibleTo` in the `.csproj` already covers the test and DynamicProxy assemblies. No type that was previously internal has been made public. PASS.

**Public interface pollution (Invariant 5):** No changes to `IMarketDataService`, `ITradingService`, `IAccountService`, or any other Core interface. PASS.

**`static class` for non-swappable pure helpers (Invariant 11):** `BitgetValueParsers` and `BitgetRequestValidation` are pure deterministic functions with no DI dependency. `BitgetSymbolFormat` is a format-data holder. These are correctly pure helpers with no swappable behavior — same as Bybit/OKX/Binance equivalents. PASS.

**Build:** `dotnet build` reports 0 Warnings, 0 Errors with `TreatWarningsAsErrors=true`. PASS.

---

## Findings

### Finding: BitgetSymbolFormat is an exact structural clone of BybitSymbolFormat
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetSymbolFormat.cs:1-20`
- **Category**: Architecture (pattern conformance / duplication)
- **Verdict**: PASS (conformance is correct; concern noted per macro-architecture mandate)
- **Issue**: `BitgetSymbolFormat.Instance` is structurally identical to `BybitSymbolFormat.Instance` — same `Delimiter=""`, `Casing=Upper`, same eight `FallbackQuoteAssets` in the same order. This is the third delimiter-less upper-case format (Binance/Bybit/Bitget). Each lives in its own assembly file with no shared base. Correct per the established pattern, but as the N-th copy it makes future `FallbackQuoteAssets` changes error-prone (edit three files, not one).
- **Fix**: No immediate action required. If a fourth delimiter-less exchange arrives, the orchestrator/maintainer should evaluate whether a shared `SymbolFormatPresets.DelimiterlessUpper(IReadOnlyList<string> fallbacks)` factory in Core reduces the blast radius of fallback-list maintenance.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/BybitSymbolFormat.cs:1-20`

---

### Finding: `ParseOptionalDecimal` maps zero to null — potential semantic loss for legitimate zero amounts
- **Severity**: MEDIUM
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Bitget/Internal/BitgetValueParsers.cs:98-104`
- **Category**: Architecture (semantic correctness)
- **Verdict**: CONCERN (non-blocking — confidence 60, pattern is shared with all three prior exchanges)
- **Issue**: `ParseOptionalDecimal` returns `null` for both missing (`""`) and genuinely-zero (`"0"`) inputs. The justification mirrors the identical rationale in `OkxValueParsers` and `BybitValueParsers`. However this assumption bakes in that no Bitget optional field legitimately carries a semantic zero (e.g. a zero-fee fill). This is a pre-existing pattern-level risk shared across OKX/Bybit/Bitget. It will not cause a defect within the three files under review, but the risk should be flagged before mapping profiles are written.
- **Fix**: No action required in TASK-020. TASK-022 unit tests should include an explicit test case asserting that `ParseOptionalDecimal("0")` returns `null` is the intended behavior, with a comment naming the Bitget fields where `"0"` means unset. If a field is discovered where zero is semantically meaningful, a `ParseZeroableDecimal` variant should be added.
- **Pattern reference**: `src/CryptoExchanges.Net.Okx/Internal/OkxValueParsers.cs:26-31`, `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs:27-33`

---

### Finding: `BitgetRequestValidation` omits `MaxHistorySpan` — cursor-pagination posture
- **Severity**: LOW
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Bitget/Internal/BitgetRequestValidation.cs:22-33`
- **Category**: Architecture (API constraint modeling)
- **Verdict**: PASS
- **Issue**: Unlike Binance (24-hour window) and Bybit (7-day window), Bitget V2 spot does not enforce a server-side fixed max time span on its cursor-paginated history endpoints (`idLessThan`). The implementation notes document this explicitly, and the posture matches `OkxRequestValidation` (also cursor-paginated, no span constraint). The validation correctly enforces only `limit ∈ [1..100]` and ordering of `startTime`/`endTime`. Acceptable and consistent.
- **Pattern reference**: `src/CryptoExchanges.Net.Okx/Internal/OkxRequestValidation.cs:31-42`

---

### Finding: `BitgetSymbolFormat` — `internal` class with `public static readonly` member
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bitget/BitgetSymbolFormat.cs:7,10`
- **Category**: Architecture (access modifier consistency)
- **Verdict**: PASS
- **Issue**: The class is `internal` and the field is `public static readonly`. The `public` on a member of an `internal` class is effectively `internal` — a known C# convention that matches Bybit/OKX exactly. No external consumer can access `BitgetSymbolFormat.Instance`. Completely consistent with the reference pattern.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/BybitSymbolFormat.cs:7,10`

---

### Finding: `ParseOrderStatus` correctly maps British `"cancelled"` to `OrderStatus.Canceled`
- **Severity**: LOW
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bitget/Internal/BitgetValueParsers.cs:73-80`
- **Category**: Architecture (wire-format correctness)
- **Verdict**: PASS
- **Issue**: Bitget V2 uses British `cancelled` while OKX uses American `canceled`. The parser correctly maps `"cancelled"` to `OrderStatus.Canceled`. The `"init"/"new"/"live"` grouping is documented as conservative inclusion; unknown statuses are non-throwing, consistent with OKX/Bybit. No issue.

---

## Summary

- PASS: `BitgetSymbolFormat` — Correct `Delimiter=""`, `Casing=Upper`, eight fallback quote assets; mirrors Bybit exactly as mandated by spec (confidence: 95/100)
- PASS: `BitgetValueParsers` — All parsers use `InvariantCulture`; wire tokens accurately reflect Bitget V2 lower-case conventions; `ParseMs` is non-throwing; `ParseOrderStatus` handles British spelling and unknown-graceful; consistent with OKX/Bybit reference patterns (confidence: 92/100)
- PASS: `BitgetRequestValidation` — `MaxHistoryLimit=100` matches Bitget V2 cap; no max-span constraint matches cursor-paginated OKX posture; validation logic correct (confidence: 95/100)
- PASS: Dependency direction — No Core, Http, DI, or cross-exchange references introduced (confidence: 99/100)
- PASS: `internal` visibility maintained — All three classes remain `internal`; no previously-private types exposed (confidence: 99/100)
- PASS: Build — 0 warnings, 0 errors with `TreatWarningsAsErrors=true` (confidence: 100/100)
- CONCERN: `ParseOptionalDecimal` zero-to-null semantic — pre-existing pattern shared with OKX/Bybit; TASK-022 tests should assert the intended behavior explicitly for any Bitget field where `"0"` has ambiguous meaning (confidence: 60/100, non-blocking)
- CONCERN (macro): Three delimiter-less upper-case `SymbolFormat` instances now exist (Binance/Bybit/Bitget) with identical `FallbackQuoteAssets`. A fourth would make maintenance of that list error-prone. No action required now but worth flagging before the next exchange milestone (confidence: 95/100, non-blocking)

---

## Final Verdict

**APPROVED**

No findings meet the blocking threshold (confidence >= 80 AND severity HIGH/MEDIUM). All three files correctly implement the established Bybit/OKX post-Binance internals pattern. The diff introduces no cross-layer coupling, no public-surface growth, no Core modifications, and no static mutable state. Build is clean.
