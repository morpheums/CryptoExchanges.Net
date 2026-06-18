# Architect Review — TASK-004
**Reviewer**: Architect Reviewer
**Task**: BybitSymbolFormat + value parsers + request validation
**Branch**: feat/m2-exchange-expansion
**Commit**: c1007cd
**Date**: 2026-06-17

---

## Files Reviewed
- `src/CryptoExchanges.Net.Bybit/BybitSymbolFormat.cs`
- `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs`
- `src/CryptoExchanges.Net.Bybit/Internal/BybitRequestValidation.cs`

## Pattern References
- `src/CryptoExchanges.Net.Binance/BinanceSymbolFormat.cs`
- `src/CryptoExchanges.Net.Binance/Internal/BinanceValueParsers.cs`
- `src/CryptoExchanges.Net.Binance/Internal/BinanceRequestValidation.cs`

---

## Findings

### Finding 1: Layering integrity — .csproj dependencies are correct
- **Severity**: N/A
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj:12-14`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. `ProjectReference` nodes point exclusively to Core and Http — matching the Binance pattern identically. No reference to any other exchange project or DI package.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/CryptoExchanges.Net.Binance.csproj:12-14`

---

### Finding 2: `BybitSymbolFormat` — `public static readonly` on an `internal` class
- **Severity**: LOW
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Bybit/BybitSymbolFormat.cs:7,10`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `BybitSymbolFormat` is declared `internal static class` but `Instance` carries `public` visibility. Because the enclosing class is `internal`, the effective accessibility of `Instance` is `internal` — this is legal and harmless. This is a consistent style quirk identical to the Binance pattern, not an introduced deviation. The member-level `public` has no observable effect but may confuse future readers.
- **Fix**: Optionally change `public static readonly` to `internal static readonly` for semantic clarity. Non-blocking given the identical Binance precedent.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/BinanceSymbolFormat.cs:10`

---

### Finding 3: `BybitValueParsers` — relies on GlobalUsings, no explicit usings
- **Severity**: LOW
- **Confidence**: 65
- **File**: `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs:1`
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: The file has no explicit `using` statements for the Core enums it references. These are pulled in via `GlobalUsings.cs`. The Binance equivalent also has no top-level `using` statements. `CultureInfo` is referenced fully-qualified inline (lines 18, 31) — identical to the Binance file. Consistent with the established pattern.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Internal/BinanceValueParsers.cs:1-3`

---

### Finding 4: `PostOnly` TIF mapped to `TimeInForce.Gtc` — semantic fidelity
- **Severity**: MEDIUM
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs:90`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: Bybit's `PostOnly` TIF is a maker-only constraint semantically distinct from GTC: a GTC order can take liquidity immediately; PostOnly is rejected if it would match. Collapsing them under `TimeInForce.Gtc` loses information. The Core `TimeInForce` enum has no `PostOnly` member, so the choice is forced by the current domain model. The manifest acknowledges this mapping decision. No architectural invariant is violated today.
- **Fix**: Add `TimeInForce.PostOnly` to the Core enum in a follow-on task (adding an enum member is non-breaking for consumers using `_` default arms). No change required in this diff.
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Enums/Enums.cs:54-63`

---

### Finding 5: `ParseOrderStatus` — `"Triggered"` maps to `OrderStatus.PendingNew`
- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs:78`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: In Bybit V5, `"Triggered"` indicates a conditional order has been triggered and is now active. `OrderStatus.PendingNew` in Core was defined for OCO-list scenarios where an order awaits activation by a partner order. The semantic mapping is a stretch. No Core enum member better represents the concept; the manifest documents the decision. No architectural rule is broken.
- **Fix**: Add an inline comment to the `"Triggered"` arm, e.g. `// Bybit: trigger order is now live; closest domain approximation is PendingNew`. Non-blocking.
- **Pattern reference**: `src/CryptoExchanges.Net.Core/Enums/Enums.cs:48`

---

### Finding 6: `BybitRequestValidation.MaxHistoryLimit` — `public const` on an `internal` class
- **Severity**: LOW
- **Confidence**: 65
- **File**: `src/CryptoExchanges.Net.Bybit/Internal/BybitRequestValidation.cs:10`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: Same structural observation as Finding 2 — `public const` on an `internal` class is redundant (effective visibility is internal). Identical to the Binance pattern.
- **Fix**: Optionally align to `internal const`. Non-blocking, consistent with existing Binance precedent.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Internal/BinanceRequestValidation.cs:10`

---

### Finding 7: No public interfaces modified, no DeltaMapper profiles, no signing, no DI, no HTTP operations
- **Severity**: N/A
- **Confidence**: 100
- **File**: All three new files
- **Category**: Architecture
- **Verdict**: PASS
- **Issue**: None. The diff is purely additive: three new `internal` files in the Bybit exchange project. No existing interface (`IMarketDataService`, `ITradingService`, `IAccountService`, `IExchangeClient`) is modified. No DeltaMapper profile is introduced (appropriate — no DTO-to-model mapping occurs here). No `DelegatingHandler`, signing logic, DI registrations, HTTP operations, or static mutable fields.

---

### Finding 8: `FallbackQuoteAssets` includes `"USDE"` — Bybit-specific stablecoin
- **Severity**: LOW
- **Confidence**: 50
- **File**: `src/CryptoExchanges.Net.Bybit/BybitSymbolFormat.cs:17`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: The Bybit fallback quote list includes `"USDE"` (Ethena USD), not present in Binance's list. This is a legitimate Bybit-specific stablecoin. The manifest notes the lists are exchange-tuned; this is a correct deviation. Ordering by liquidity is reasonable.
- **Fix**: Confirm the list matches Bybit V5 spot market quote assets during integration testing (TASK-008). No change required in this diff.

---

### Finding 9: `ParseDecimal` parameter type is `string` not `string?`
- **Severity**: LOW
- **Confidence**: 72
- **File**: `src/CryptoExchanges.Net.Bybit/Internal/BybitValueParsers.cs:14`
- **Category**: Architecture
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `ParseDecimal(string value)` takes a non-nullable `string` but guards against `null` internally via `string.IsNullOrEmpty`. The signature does not express nullable intent in the type system. This is identical to the Binance file and is therefore a consistent project-wide pattern, not an introduced regression.
- **Fix**: No change required given established Binance precedent. If the team aligns on `string?`, apply to both exchange projects simultaneously.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Internal/BinanceValueParsers.cs:14`

---

## Build Verification

`dotnet build CryptoExchanges.Net.sln` on branch `feat/m2-exchange-expansion`: **Build succeeded. 0 Warning(s). 0 Error(s).**

---

## Summary

- PASS: Layering (csproj) — Core + Http references only; no exchange cross-contamination
- PASS: `internal` encapsulation — all three new types are `internal`; no exchange internals exposed as `public`
- PASS: No public interface modifications — `IMarketDataService`, `ITradingService`, `IAccountService`, `IExchangeClient` untouched
- PASS: No DeltaMapper profiles needed — no DTO-to-model mappings in scope; correct
- PASS: Core value types used correctly — `SymbolFormat`, `SymbolCasing`, `Asset`, all Core enums via GlobalUsings; no redefinitions
- PASS: Binance pattern fidelity — structure of all three files mirrors Binance equivalents exactly
- PASS: Documented deviations are architecturally sound — V5 mixed-case enum tokens, 50/7-day history constants, two-type `ParseOrderType` all follow actual Bybit V5 wire format
- PASS: No global/static mutable state introduced
- PASS: Build clean (0 warnings, 0 errors with `TreatWarningsAsErrors=true`)
- CONCERN: `PostOnly` → `Gtc` mapping loses semantic precision (confidence: 60/100, non-blocking)
- CONCERN: `"Triggered"` → `OrderStatus.PendingNew` is a loose approximation; inline comment recommended (confidence: 55/100, non-blocking)
- CONCERN: `public` member visibility on `internal` classes is redundant but consistent with Binance precedent (confidence: 65-70/100, non-blocking)

---

## Final Verdict

**APPROVED**

No blocking findings. All three files correctly implement the Binance mirror pattern: proper Core value type reuse (`SymbolFormat`/`SymbolCasing`/domain enums), `internal` encapsulation, `InvariantCulture` parsing, deterministic exception strategy for mandatory fields vs. graceful fallback for status, and Bybit V5-specific constants (50/7-day). The four documented deviations in the manifest (mixed-case wire tokens, `PostOnly`-to-GTC, two-type order parser, 50/7-day history window) are architecturally sound and accurately reflect the Bybit V5 API contract. The two semantic approximation concerns (`PostOnly`/`Triggered` mappings) are domain-model gaps deferred to a Core enum extension task, not violations of any architectural invariant in this diff.
