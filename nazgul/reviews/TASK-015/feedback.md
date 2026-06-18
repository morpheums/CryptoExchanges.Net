# Consolidated Review Feedback — TASK-015 (OKX, closes M-OKX)

**Gate round**: 1 (redo after prior session limit)
**Aggregate verdict**: CHANGES_REQUESTED
**Branch**: feat/m3-okx · HEAD b78be03 (includes simplify commit)

## Per-reviewer verdicts
| Reviewer | Verdict | Confidence |
|----------|---------|------------|
| architect-reviewer | APPROVED | 92 |
| code-reviewer | **CHANGES_REQUESTED** | 95 |
| security-reviewer | APPROVED | 95 |
| api-reviewer | APPROVED | (MEDIUM/90 finding, self-classified non-blocking) |

`require_all_approve = true` → one CHANGES_REQUESTED makes the aggregate CHANGES_REQUESTED.

---

## BLOCKING items (must fix) — both AUTO-FIX (mechanical, non-security)

### B1 — Unguarded `long.Parse` on candlestick timestamp [HIGH / 95]
- **File**: `src/CryptoExchanges.Net.Okx/Services/OkxMarketDataService.cs:214`
- **Issue**: `long.Parse(arr[0], CultureInfo.InvariantCulture)` throws `FormatException` on a null/empty/non-numeric `ts` (OKX returns `""` for unconfirmed candles). Every other timestamp in the file uses the safe `OkxValueParsers.ParseMs` (`TryParse` → `0L`); this is the lone asymmetry. The exception escapes `GetCandlesticksAsync` uncaught.
- **Fix**: replace line 214 with
  ```csharp
  OpenTime: DateTimeOffset.FromUnixTimeMilliseconds(OkxValueParsers.ParseMs(arr[0])),
  ```

### B2 — `GetCandlesticksAsync` has zero test coverage [MEDIUM / 95]
- **File**: `tests/CryptoExchanges.Net.Okx.Tests.Unit/OkxMappingAndServiceTests.cs`
- **Issue**: The only service method with no test; it is exactly where B1's bug lives (and the `MapKlineInterval` 8h-throws path is untested at service level). `GetOrderBook` and `GetRecentTrades` are both tested.
- **Fix**: add at minimum:
  1. Happy-path: mock a 7-element array response, assert `OpenTime`/`Open`/`Volume`.
  2. `EightHours` interval → `ArgumentOutOfRangeException`.
  3. `limit=500` → clamped to "100" (mirror `Account_GetTradeHistory_DefaultLimit_ClampsToHundred`).

---

## NON-BLOCKING concerns (auto-approved, <80 conf or non-blocking) — do NOT gate on these
- **api MEDIUM/90**: `OkxOptions.ToCredentials()` throws `ArgumentException` with default empty `Passphrase`; never called in signing path. Optional: guard empty→null, or make `internal`.
- **api LOW/75 & LOW/70**: `PostAsync<T>` overload divergence vs Bybit; `ToCredentials()` is effectively dead public API.
- **code MEDIUM/60**: `TryMapTicker` catches only `FormatException` — document scope or widen with justification.
- **code LOW/55**: `CA1031` project-level suppression without per-site docs (consistent w/ Bybit/Binance).
- **architect LOW/60**: `public` members inside `internal OkxRequestValidation` (effectively internal; cosmetic).
- **architect LOW/55**: `public` ctor on `internal OkxResponseProfile` (pre-existing Bybit pattern; verify DeltaMapper need).
- **architect LOW/70**: stale duplicate comment in `OkxHttpClient.PostJsonAsync`.
- **security LOW**: `OkxOptions` no explicit `ToString()` redaction (plain class, not record — low risk today).

---

## Architect milestone-boundary macro note (M-OKX closer — recorded, NOT blocking)
Three exchanges (Binance, Bybit, OKX) now DONE; ADR-001 correctly applied; no blocking structural debt. Four latent duplications that COMPOUND with Bitget:
1. `ServiceCollectionExtensions` ×3 — ~375 lines, ~90% identical (Bybit↔OKX differ by ~8 lines). +120 with Bitget.
2. `XxxClientComposer` ×3 — five-method skeleton near-identical; only `BuildResilientHttpClient` diverges.
3. `CryptoExchangesOptions` — +3-4 nullable strings per exchange; ~16+ at Bitget.
4. `XxxTimeSync` ×3 — identical `ComputeOffset`/`ApplyOffset`; zero exchange-specific logic.

Recommended BEFORE Bitget (priority order): (a) extract `TimeSync` into Core (lowest-effort/highest-leverage); (b) shared DI helper for the keyed-singleton registration block; (c) accept Composer duplication for now. Suggest a dedicated `TASK-REF-001: Extract per-exchange DI helper` before M-BITGET starts.
