# Learned Rules

<!-- Human-approved rules distilled from recurring mistakes. Managed by /nazgul:learn. Never renumber; retire via Status. -->

---

## LR-001: Guard string parameters with ArgumentException.ThrowIfNullOrWhiteSpace at every method boundary
- **Status**: active
- **Scope-Agents**: implementer, code-reviewer
- **Scope-Globs**: src/**/*.cs
- **Hits**: 9
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
- **Hits**: 1
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
- **Hits**: 5
- **Added**: 2026-06-18
- **Evidence**: TASK-007 (api-reviewer REJECT@95 — BybitTimeSync.ApplyOffset null-checked offsetHolder but did not guard Length, causing IndexOutOfRangeException on new long[0]), TASK-015 (code-reviewer PASS@100 — OkxTimeSync.ApplyOffset correctly adds zero-length guard; unit test ApplyOffset_RejectsZeroLengthHolder), TASK-022 (code-reviewer PASS@100 — BitgetTimeSync.ApplyOffset zero-length guard confirmed present), TASK-059 (api-reviewer REJECT@95 — KucoinClientComposer.BuildResilientHttpClient null-checked offsetHolder but missed the Length guard before offsetHolder[0] access inside the signing closure; Fix-First commit ee97d43 added the guard + Array.Empty<long>() test)

When a method accepts an array parameter and accesses it by index (e.g.,
`Interlocked.Exchange(ref offsetHolder[0], offset)`), apply two guards in order:
(1) `ArgumentNullException.ThrowIfNull(offsetHolder)` and then
(2) `if (offsetHolder.Length < 1) throw new ArgumentException("offsetHolder must have at least one element.", nameof(offsetHolder));`.
A null-only guard is insufficient — a zero-length array passes the null check and then
produces an `IndexOutOfRangeException` with no diagnostic information. Both guards must be
present and must precede the indexed access. This applies not only to `TimeSync.ApplyOffset`
but to **every** indexed array-parameter site — including composer `BuildResilientHttpClient`
methods where `offsetHolder[0]` is captured inside a signing closure. The pattern is now
established across Bybit, OKX, Bitget, and KuCoin `TimeSync.ApplyOffset` and the per-exchange
client composers.

---

## LR-005: Every new service method must have at least one unit test; zero coverage is a REJECT finding
- **Status**: active
- **Scope-Agents**: implementer, code-reviewer
- **Scope-Globs**: src/**/Services/*.cs, tests/**/*.cs
- **Hits**: 7
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

---

## LR-006: Type a signing-handler constructor parameter to a narrow interface, never the concrete signature service
- **Status**: active
- **Scope-Agents**: implementer, architect-reviewer, api-reviewer
- **Scope-Globs**: src/**/Resilience/*.cs, src/**/Auth/*.cs
- **Hits**: 1
- **Added**: 2026-06-21
- **Evidence**: TASK-057 Cycle 1 (architect-reviewer REJECT@95 + api-reviewer REJECT@95 — KucoinSigningHandler constructor typed as concrete KucoinSignatureService instead of an interface, violating DIP Invariant 11; fixed Cycle 2 by introducing IKucoinSignatureService : ISignatureService)

When an exchange's signing handler needs a capability not present on the shared
`ISignatureService` (e.g. KuCoin's `SignPassphrase(string)` for passphrase-v2), do NOT type the
handler's injected constructor parameter as the concrete service class. Create a narrow
`internal interface IXxxSignatureService : ISignatureService` in the exchange's `Auth/` folder
adding only the extra members, implement it on the concrete class, and type the handler
parameter to the interface. The fix is always three files: create the interface, update the
`implements` clause, update the constructor parameter. Concrete-type injection is a 2-reviewer
REJECT@95 — it violates DIP and makes the handler unmockable. Static pure helpers on the
concrete class (`FormatTimestamp`, `BuildPrehash`) may still use static dispatch; only injected
instance dependencies must be interface-typed. Reference:
`src/CryptoExchanges.Net.Kucoin/Auth/IKucoinSignatureService.cs`.

---

## LR-007: Every public *Options property must be demonstrably consumed by the DI factory, with a test proving it
- **Status**: active
- **Scope-Agents**: implementer, api-reviewer
- **Scope-Globs**: src/**/*Options.cs, src/**/ServiceCollectionExtensions.cs, src/**/StreamServiceCollectionExtensions.cs
- **Hits**: 1
- **Added**: 2026-06-21
- **Evidence**: TASK-062 Cycle 1 (api-reviewer REJECT@98 — KucoinStreamOptions.RestBaseUrl was public and ValidateOnStart-registered but never read by the protocol factory; setting it was a silent no-op; fixed Cycle 2 by resolving the options in the factory and threading the value through, with a wiring test)

Every property on a public `*Options` class must demonstrably control runtime behaviour in the
DI factory that registers it. Declaring a property, wiring `ValidateOnStart`, then not reading
it in the factory lambda creates a misleading public surface — callers configure the option and
observe no effect with no error. In the factory lambda, resolve the options
(`sp.GetRequiredService<XxxOptions>()`), apply LR-001 guards, validate URI shape for URL
properties (`Uri.TryCreate(..., UriKind.Absolute, out _)`), and thread the validated value into
the produced object (e.g. `httpClient.BaseAddress = baseUri`). Add a no-network unit test
proving a custom option value is the actual runtime value used. Reference:
`KucoinStreamOptionsWiringTests`.

---

## LR-008: A new exchange's DI task must wire both MCP registration points (ToolInputs + EnvCredentialBinder) in the same cycle
- **Status**: active
- **Scope-Agents**: implementer, code-reviewer
- **Scope-Globs**: src/CryptoExchanges.Net.Mcp/ToolInputs.cs, src/CryptoExchanges.Net.Mcp/EnvCredentialBinder.cs
- **Hits**: 1
- **Added**: 2026-06-21
- **Evidence**: TASK-060 shipped AddKucoinExchange but left ToolInputs.cs and EnvCredentialBinder.cs without KuCoin entries; the gap surfaced at TASK-064 Cycle 1 (code-reviewer REJECT@95 — docs advertised the `kucoin` MCP key but routing returned ExchangeUnavailable; Fix-First commit d54e9f1 wired both)

When a new exchange ships its `AddXxxExchange` DI extension, the same task must update both MCP
registration points, or MCP tool invocation silently fails while DI succeeds:
1. `src/CryptoExchanges.Net.Mcp/ToolInputs.cs` — add the exchange-key routing entry
   (e.g. `["kucoin"] = ExchangeId.Kucoin`).
2. `src/CryptoExchanges.Net.Mcp/EnvCredentialBinder.cs` — add all exchange env-var bindings
   (e.g. `KUCOIN_API_KEY`, `KUCOIN_SECRET_KEY`, `KUCOIN_PASSPHRASE`).
These two files are a checklist that travels with every new exchange's DI task. Deferring them
to a docs task forces a REJECT — the docs cannot honestly advertise the key as functional.

---

## LR-009: Verify a new streaming DTO's field CLR types and channel against the real wire before writing fixtures
- **Status**: active
- **Scope-Agents**: implementer, code-reviewer
- **Scope-Globs**: src/**/Dtos/Streaming/*.cs, tests/**/Streaming/*.cs
- **Hits**: 1
- **Added**: 2026-06-21
- **Evidence**: TASK-062 bugfix cycle — StreamTickerDto.Time and StreamKlineDto.Time typed as `string` but the live wire sends JSON numbers (silent deserialize failure), and the ticker subscribed to `/market/ticker` (no `symbol`, no 24h stats) instead of `/market/snapshot`, crashing the decoder; all three bugs were invisible to unit tests (fixtures match the DTO, not reality) and only surfaced when live integration tests timed out

Unit-test fixtures are shaped to whatever the DTO expects, so a DTO↔wire mismatch is invisible
until a live integration test runs. When writing a new streaming DTO by cloning another
exchange's shape: (1) verify every field's JSON value type against a captured live frame or the
exchange WS docs — a JSON number must be `long`/`decimal`/`int`, never `string` (a `string` field
receiving a number silently deserializes to default and breaks downstream routing/mapping);
(2) verify the subscribe channel/topic actually carries the payload shape you expect (KuCoin
`/market/ticker` vs `/market/snapshot` differ in schema, nesting depth `data` vs `data.data`, and
fields); (3) confirm types against a real frame before writing the first fixture — discovering
the mismatch post-review costs a full bugfix cycle (DTO + mapping profile + decoder + every
fixture). Capturing one live frame (a few lines of WS client) is the cheapest verification.

---

## LR-010: No self-evident XML <remarks> or dead `_ = x` assertion variables in test files
- **Status**: active
- **Scope-Agents**: implementer, code-reviewer
- **Scope-Globs**: tests/**/*.cs
- **Hits**: 1
- **Added**: 2026-06-21
- **Evidence**: TASK-063 Cycle 1 (code-reviewer REJECT@90 — KucoinStreamingSmokeTests had a <remarks> block restating visible code: "no Thread.Sleep — uses TaskCompletionSource"; REJECT@85 — two boolean locals reconnectingFired/reconnectedFired silenced with `_ = x` discards, never asserted)

Test files break the LEAN-comment mandate in two recurring ways that draw blocking REJECTs:
(1) `<remarks>` that restate what the code already shows — documenting "no Thread.Sleep, uses
TaskCompletionSource" on a class that visibly does exactly that adds nothing. A valid `<remarks>`
adds context invisible from the code (e.g. "excluded from CI via the Category trait"). (2)
Boolean locals declared in lifecycle callbacks and silenced with `_ = x` discards instead of
being asserted — `_ = x` is a CS0219 dead-variable suppressor, not a test assertion. Replace
dead structural-wiring variables with explicit no-op lambdas
(`OnReconnecting: () => ValueTask.CompletedTask`) so the wiring compiles without accumulating
unasserted state.
