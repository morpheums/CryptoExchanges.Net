# Learned Rules

<!-- Human-approved rules distilled from recurring mistakes. Managed by /nazgul:learn. Never renumber; retire via Status. -->

---

## LR-001: Guard string parameters with ArgumentException.ThrowIfNullOrWhiteSpace at every method boundary
- **Status**: active
- **Scope-Agents**: implementer, code-reviewer
- **Scope-Globs**: src/**/*.cs
- **Hits**: 8
- **Added**: 2026-06-18
- **Evidence**: TASK-002 (code-reviewer REJECT@85 — BuildGetSignString/BuildPostSignString missing guards), TASK-005 (code-reviewer REJECT@97 — BybitHttpClient.GetAsync/PostAsync/DeleteAsync missing endpoint guard), TASK-003 (api-reviewer non-blocking concern on ctor params)

Every public or internal method that accepts a non-optional string parameter must call
`ArgumentException.ThrowIfNullOrWhiteSpace(param)` as its first statement before any logic
or resource acquisition. Use `ArgumentNullException.ThrowIfNull` instead only when an empty
string is semantically valid for that parameter (e.g., `queryString` on a parameterless GET
or `jsonBody` on an empty-body POST). The project mandate is explicit (reference:
`SymbolMapper.cs:76`). Omitting these guards causes opaque downstream failures — a malformed
HMAC sign-string is silently sent to the exchange, and the root cause surfaces as an auth
error rather than a clean `ArgumentException` at the call boundary.

---

## LR-002: Clamp exchange-specific limit caps with Math.Min before validation; never throw on the interface default
- **Status**: active
- **Scope-Agents**: implementer, code-reviewer, api-reviewer
- **Scope-Globs**: src/**/Services/*.cs
- **Hits**: 0
- **Added**: 2026-06-18
- **Evidence**: TASK-006 (code-reviewer REJECT@98 + api-reviewer REJECT@95 — BybitTradingService.GetOrderHistoryAsync and BybitAccountService.GetTradeHistoryAsync with limit=500 default always threw via ValidateHistoryWindow), TASK-013 (code-reviewer LOW@45 reminder that future OKX services must clamp before calling), TASK-015 (code-reviewer verified OkxTradingService/OkxAccountService clamp correctly)

When an exchange imposes a per-call result limit lower than the interface default (e.g.,
interface default = 500, exchange max = 50 or 100), apply `Math.Min(limit, MaxHistoryLimit)`
before passing to `ValidateHistoryWindow` and before building the wire query parameter. The
clamped value must be used for both the guard call and the actual request parameter. Passing
the raw interface default to a hard-throw validator violates the Liskov Substitution Principle
and makes every default call pattern fail silently. The safe pattern:
`var effectiveLimit = Math.Min(limit, XxxRequestValidation.MaxHistoryLimit);` followed by
`ValidateHistoryWindow(effectiveLimit, ...)`.

---

## LR-003: Use the per-exchange ParseMs safe helper for all millisecond-epoch timestamp parsing
- **Status**: active
- **Scope-Agents**: implementer, code-reviewer
- **Scope-Globs**: src/**/Services/*.cs, src/**/Mapping/*.cs
- **Hits**: 1
- **Added**: 2026-06-18
- **Evidence**: TASK-015 (code-reviewer REJECT@95 — OkxMarketDataService.cs:214 used raw `long.Parse` for candlestick timestamp while every other timestamp in the same file used `OkxValueParsers.ParseMs`; OKX returns `""` for unconfirmed candles, causing FormatException), TASK-022 (code-reviewer PASS@100 confirmed BitgetMarketDataService candlestick path correctly uses `BitgetValueParsers.ParseMs`)

All millisecond-epoch timestamp strings from exchange wire responses must be parsed via the
per-exchange `ParseMs` helper (`long.TryParse` with `InvariantCulture`, returns `0L` on
failure) rather than `long.Parse(…)`. Raw `long.Parse` throws `FormatException` on null,
empty, or non-numeric strings — exchanges sometimes return `""` for unconfirmed or
in-progress data points (documented for OKX candles). The asymmetry between one unguarded
parse and every other safe parse in the same file is a pattern the code-reviewer will flag as
a REJECT. Correct reference: `OkxValueParsers.ParseMs` / `BitgetValueParsers.ParseMs`.

---

## LR-004: Guard array parameters for both null and minimum required length before indexed access
- **Status**: active
- **Scope-Agents**: implementer, api-reviewer
- **Scope-Globs**: src/**/*.cs
- **Hits**: 2
- **Added**: 2026-06-18
- **Evidence**: TASK-007 (api-reviewer REJECT@95 — BybitTimeSync.ApplyOffset null-checked offsetHolder but did not guard Length, causing IndexOutOfRangeException on new long[0]), TASK-015 (code-reviewer PASS@100 — OkxTimeSync.ApplyOffset correctly adds zero-length guard; unit test ApplyOffset_RejectsZeroLengthHolder), TASK-022 (code-reviewer PASS@100 — BitgetTimeSync.ApplyOffset zero-length guard confirmed present)

When a method accepts an array parameter and accesses it by index (e.g.,
`Interlocked.Exchange(ref offsetHolder[0], offset)`), apply two guards in order:
(1) `ArgumentNullException.ThrowIfNull(offsetHolder)` and then
(2) `if (offsetHolder.Length < 1) throw new ArgumentException("offsetHolder must have at least one element.", nameof(offsetHolder));`.
A null-only guard is insufficient — a zero-length array passes the null check and then
produces an `IndexOutOfRangeException` with no diagnostic information. Both guards must be
present and must precede the indexed access. The pattern is now established across Bybit,
OKX, and Bitget `TimeSync.ApplyOffset`.

---

## LR-005: Every new service method must have at least one unit test; zero coverage is a REJECT finding
- **Status**: active
- **Scope-Agents**: implementer, code-reviewer
- **Scope-Globs**: src/**/Services/*.cs, tests/**/*.cs
- **Hits**: 5
- **Added**: 2026-06-18
- **Evidence**: TASK-015 (code-reviewer REJECT@95 — GetCandlesticksAsync had zero test coverage; the missing test was paired with and hid the unguarded long.Parse bug at the exact same site), TASK-006 (api-reviewer/code-reviewer blocked on service methods with broken default-parameter behaviour that was undetected by insufficient test coverage)

Each new service method (`GetCandlesticksAsync`, `GetOrderBookAsync`, `GetRecentTradesAsync`,
`GetOrderHistoryAsync`, etc.) must have at least one unit test covering the happy path before
the task can pass the review gate. Zero coverage on a service method is a REJECT-level
finding regardless of whether other tests pass. The risk is compounded: an untested method is
the precise site where latent parse bugs (missing guards, wrong parse helper) are discovered
during code review rather than by a failing test. The minimum bar is a happy-path test that
mocks the HTTP client, verifies the mapped output, and confirms limit-clamping behaviour.
Reference pattern: `OkxMappingAndServiceTests` — every service method has a corresponding
`[Fact]` or `[Theory]`.
