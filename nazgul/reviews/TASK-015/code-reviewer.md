# Code Review: TASK-015 — OKX services + mapping + error + time + tests

**Reviewer**: Code Reviewer (automated)
**Date**: 2026-06-18
**Branch**: feat/m3-okx (HEAD b78be03, simplify commit included)
**Build**: `dotnet build CryptoExchanges.Net.sln` → 0 warnings, 0 errors (TreatWarningsAsErrors=true)
**Tests**: `dotnet test --filter 'Category!=Integration'` → 332 passed (91 OKX unit), 0 failed

---

## Systematic Checks

### Correctness
- [x] All new `async` methods use `.ConfigureAwait(false)` on every `await` — verified across all three services, OkxExchangeClient, OkxHttpClient
- [x] All new `async` methods accept and forward `CancellationToken ct` — all service methods confirmed
- [x] `OperationCanceledException` is re-thrown in `PingAsync` when `ct.IsCancellationRequested` — `OkxExchangeClient.cs:482-485`
- [x] `using var` / `await using` — `StringContent` and `HttpRequestMessage` use `using var` in `OkxHttpClient.cs`

### Null safety
- [x] `ArgumentNullException.ThrowIfNull()` on all public/internal reference-type params — `OkxClientComposer`, `OkxExchangeClient`, `OkxTimeSync.ApplyOffset` all correct
- [x] `ArgumentException.ThrowIfNullOrWhiteSpace()` for strings — `OkxHttpClient.PostAsync` overloads, `OkxResponseProfile` constructor, all correct
- [x] Nullable property accesses use `?.` or null-check before access — correct throughout

### Silent failure hunting
- [x] `OkxExchangeClient.PingAsync` `catch {}` block is documented (mirrors Binance pattern, catches `ExchangeException` + all) — `OkxExchangeClient.cs:491-493`
- [x] `TryMapTicker` catches `FormatException` and yields break — swallowing is intentional and documented, used only for full-universe ticker batch
- [ ] **FINDING**: `long.Parse(arr[0], ...)` in `GetCandlesticksAsync` — see Finding 1
- [x] No fire-and-forget Tasks without justification

### XML documentation
- [x] All new `public` types have `/// <summary>` — `OkxExchangeClient`, `OkxOptions`, `ServiceCollectionExtensions`
- [x] All new `public` members have docs — verified on `OkxExchangeClient` properties and methods
- [x] `internal` types: CS1591 is `<NoWarn>`-suppressed in `CryptoExchanges.Net.Okx.csproj` with comment "Suppress analysis noise for HTTP client / JSON deserialization library" — acceptable given all-internal architecture

### Code style
- [x] Primary constructors for all three services — `OkxMarketDataService`, `OkxTradingService`, `OkxAccountService`
- [x] `sealed record` for all DTOs — correct
- [x] No new `public` mutable fields — confirmed
- [x] Collection expressions `[.. items]` used — confirmed

### Roslyn analyzer compliance
- [x] Build clean — 0 warnings, 0 errors
- [x] `#pragma warning disable CA1861` in `ServiceCollectionExtensions.cs` is paired and justified with comment explaining singleton factory semantics
- [x] `[SuppressMessage]` on `_http` field includes `Justification` — `OkxExchangeClient.cs:400`

---

## Judgment Flag Verification

### 1. DeltaMapper mandate
PASS. `OkxResponseProfile` is a DeltaMapper `Profile` in `Mapping/OkxMappingProfiles.cs`. `AssertConfigurationIsValid()` is invoked in `OkxClientComposer.CreateMapper` (line 157). Unit-tested in `OkxMappingAndServiceTests.MapperConfiguration_IsValid`. No AutoMapper. No hand-rolled mapping in services except the documented Trade/OrderBook/Candlestick direct-build precedent (consistent with Bybit/Binance).

### 2. JSON reads ValueKind-guarded
PASS. `OkxErrorTranslator.ReadString` correctly guards with `v.ValueKind == JsonValueKind.String` before calling `v.GetString()` — both `code` and `msg` fields go through this helper. The `data[0]` element checks `first.ValueKind == JsonValueKind.Object` before property access. `JsonDocument.Parse` is wrapped in `catch (JsonException)`. No `InvalidOperationException` can escape.

### 3. Limits clamped
PASS.
- Books: `Math.Clamp(depth, 1, 400)` — `OkxMarketDataService.cs:166`
- Candles: `Math.Min(limit, OkxRequestValidation.MaxHistoryLimit)` (MaxHistoryLimit=100) — `OkxMarketDataService.cs:195`
- Trades: `Math.Clamp(limit, 1, 500)` — `OkxMarketDataService.cs:247`
- Trade history: `Math.Min(limit, OkxRequestValidation.MaxHistoryLimit)` — `OkxAccountService.cs:951`
- Order history: `Math.Min(limit, OkxRequestValidation.MaxHistoryLimit)` — `OkxTradingService.cs:1566`

### 4a. 8h bar throws / supported bar set
PASS. `MapKlineInterval` covers `OneMinute` through `OneMonth` (14 explicit arms). `KlineInterval.EightHours` exists in the core enum but has no arm in the switch, so it falls to `_ => throw new ArgumentOutOfRangeException(...)` — correct behavior per comment "OKX V5 spot candles do not expose an 8h bar."

### 4b. CumulativeQuoteQuantity = accFillSz × avgPx
PASS. Mapping at `OkxMappingProfiles.cs:322-323`: `OkxValueParsers.ParseDecimal(s.AccFillSz) * OkxValueParsers.ParseDecimal(s.AvgPx)`. Unit-tested: `OrderProfile_MapsAllScalarsAndResolvesSymbol` asserts `1 * 150.75 = 150.75m`; `OrderProfile_MarketOrder_MapsCleanly` asserts `0.5 * 42000 = 21000m`.

### 4c. Ticker PriceChangePercent = (last-open24h)/open24h*100, divide-by-zero guard
PASS. Mapping at `OkxMappingProfiles.cs:336-339`: explicit `== 0m` guard returns `0m`. Unit-tested with `5%` assertion.

### 4d. TimeInForce and Type both key off ordType, both parsers accept "market"
PASS. `ParseOrderType("market") -> Market`; `ParseTimeInForce("market") -> Ioc`. Both tested by `ParseOrderTypeAndTimeInForce_BothAccept_Market` and the `OrderProfile_MarketOrder_MapsCleanly` regression.

### 5. Test quality

#### base64 sig vector
PASS. `Sign_ProducesExpectedBase64ForFixedVector` asserts exact `iKqz7ejTrflNJquQ07r9SiCDBww7zOnAFO4EpEOEfAs=`. `Sign_MatchesCoreHmacBase64` verifies consistency with `HmacSignature.Compute(..., Base64)`.

#### Prehash assembly (GET path+query vs POST body)
PASS. `BuildPrehash_Get_AssemblesTimestampMethodPathQuery` and `BuildPrehash_Post_AssemblesTimestampMethodPathBody` assert exact prehash strings with known vectors.

#### Four-header signing + re-sign-on-retry
PASS. Integration: `SignedGet_SetsFourOkAccessHeaders` checks all four OK-ACCESS-* headers including base64 regex. `SignedGet_Retried_ReSignsWithSingleHeaderSet` uses `SeqStub` (500→200), confirms 2 attempts each with exactly one set of auth headers (no `,` in values).

#### Passphrase-missing fast-fail
PASS. Integration: `PassphraseMissing_SignedRequest_FastFails` constructs `OkxSigningHandler` with empty passphrase and expects `InvalidOperationException`.

#### Hyphen symbol round-trip
PASS. `Symbol_ToWire_UsesHyphenUpperCase` → `"BTC-USDT"`; `Symbol_FromWire_RoundTrips` confirms round-trip.

#### Parsers + validation
PASS. Comprehensive `[Theory]` coverage for `ParseDecimal`, `ParseOptionalDecimal`, `ParseAssetOrNone`, `ParseOrderSide`, `ParseOrderType`, `ParseOrderStatus`, `ParseTimeInForce`. `ValidateHistoryWindow` tested for in-range, out-of-range (0, -1, 101, 500), and inverted window.

#### Per-service DeltaMapper mapping over mocked IOkxHttpClient
PASS. `MarketData_GetTickers_SingleSymbol_MapsPayload`, `MarketData_GetOrderBook_ParsesLevels`, `Account_GetBalances_TrimsZeroBalances`, `Account_GetTradeHistory_DefaultLimit_ClampsToHundred`, `Trading_GetOpenOrders_MapsOrders`, `Trading_GetOrderHistory_DefaultLimit_ClampsToHundred`.

#### Error mapping (code=="0" not an error + per-order sCode)
PASS. `ErrorTranslator_SuccessEnvelope_IsNotARateLimitOrAuthError`, `ErrorTranslator_PerOrderSCode_IsClassifiedWhenTopLevelCodeIsSuccess`, `ErrorTranslator_PerOrderSCodeZero_IsNotAnError`. All critical cases tested.

#### Time-sync + zero-length holder guard
PASS. `ComputeOffset_ReturnsServerMinusLocal`, `ApplyOffset_WritesIntoHolderAndReturnsOffset`, `ApplyOffset_RejectsZeroLengthHolder`.

#### DI resolution
PASS. 8 DI tests covering keyed client, secretless, passphraseless, mapper singleton, invalid options fail-fast, multi-exchange resolution, scope validation.

#### MARKET-ORDER round-trip regression
PASS. `Trading_PlaceMarketOrder_SendsMarketOrdTypeAndRefetches` asserts `ordType=market`, no `px` key, re-fetches order with correct id.

#### Candlestick timestamp parse path coverage
MISSING. No test exercises `GetCandlesticksAsync` — this is directly relevant to Finding 1 below.

---

## Findings

### Finding 1: Unguarded `long.Parse` on candlestick timestamp — can throw `FormatException` on malformed wire payload

- **Severity**: HIGH
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Okx/Services/OkxMarketDataService.cs:214`
- **Category**: Correctness
- **Verdict**: REJECT

**Issue**: Line 214 uses `long.Parse(arr[0], System.Globalization.CultureInfo.InvariantCulture)` directly to parse the candlestick timestamp from OKX's wire response (`arr[0]`). This throws `FormatException` if the API returns a null, empty, or non-numeric string in position 0. The `ParseMs` helper (`OkxValueParsers.cs:270`) exists precisely to handle this safely: it uses `long.TryParse` and returns `0L` on failure. Every other timestamp parse in the same file goes through `ParseMs` (line 176: `OkxValueParsers.ParseMs(book.Ts)`, line 259: `OkxValueParsers.ParseMs(t.Ts)`). This is an asymmetry within the same file. The `FormatException` would escape through `GetCandlesticksAsync` to callers, which only catch `FormatException` in `TryMapTicker` — not in the candlestick path. OKX occasionally returns `""` in the ts field for unconfirmed candles.

There is also **no test** covering `GetCandlesticksAsync` at all, so the parse path is entirely untested.

**Fix**:
```csharp
// Replace line 214:
OpenTime: DateTimeOffset.FromUnixTimeMilliseconds(long.Parse(arr[0], System.Globalization.CultureInfo.InvariantCulture)),

// With:
OpenTime: DateTimeOffset.FromUnixTimeMilliseconds(OkxValueParsers.ParseMs(arr[0])),
```

And add a unit test:
```csharp
[Fact]
public async Task MarketData_GetCandlesticks_ParsesTimestampAndOhlcv()
{
    var (symbolMapper, mapper) = BuildMappers();
    var http = Substitute.For<IOkxHttpClient>();
    http.GetAsync<OkxResponse<List<string>>>(
            "/api/v5/market/candles", Arg.Any<Dictionary<string, string>>(), false, Arg.Any<CancellationToken>())
        .Returns(new OkxResponse<List<string>>
        {
            Data = [["1700000000000", "42000", "43000", "41000", "42500", "10", "420000"]]
        });

    var service = new OkxMarketDataService(http, symbolMapper, mapper);
    var candles = await service.GetCandlesticksAsync(BtcUsdt, KlineInterval.OneHour, ct: TestContext.Current.CancellationToken);

    candles.Should().HaveCount(1);
    candles[0].OpenTime.Should().Be(DateTimeOffset.FromUnixTimeMilliseconds(1700000000000));
    candles[0].Open.Should().Be(42000m);
    candles[0].Volume.Should().Be(10m);
}
```

**Pattern reference**: `OkxMarketDataService.cs:176` (`OkxValueParsers.ParseMs(book.Ts)`), `OkxValueParsers.cs:270` (the `ParseMs` implementation using `long.TryParse`)

---

### Finding 2: `TryMapTicker` catches only `FormatException` — `InvalidOperationException` from DeltaMapper member mapping would escape

- **Severity**: MEDIUM
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Okx/Services/OkxMarketDataService.cs:306-316`
- **Category**: Correctness
- **Verdict**: CONCERN (confidence < 80, non-blocking)

**Issue**: `TryMapTicker` catches `FormatException` to skip unresolvable symbols in the full-universe batch. However, if `symbolMapper.FromWire` throws a `KeyNotFoundException` or `FormatException` with a different type (or DeltaMapper internally rethrows another exception type), it would escape. The scope is intentionally narrow to `FormatException` — but DeltaMapper mapping failures may manifest as other types. The existing Bybit `TryMapTicker` pattern should be verified as the reference. Confidence is 60 because DeltaMapper's documented failure mode for unresolvable symbol strings appears to be `FormatException` per the profile's `symbolMapper.FromWire` call chain.

**Fix**: Either document why only `FormatException` is sufficient (referencing the specific DeltaMapper/FromWire exception contract), or widen the catch to `Exception` with a justification comment (matching the documented `PingAsync` precedent for deliberate broad catches).

---

### Finding 3: `CA1031` suppressed globally in `OkxOptions`/library csproj without per-site documentation

- **Severity**: LOW
- **Confidence**: 55
- **File**: `src/CryptoExchanges.Net.Okx/CryptoExchanges.Net.Okx.csproj:8`
- **Category**: Code Quality
- **Verdict**: CONCERN (non-blocking)

**Issue**: `CA1031` is suppressed at project level with comment "Suppress analysis noise for HTTP client / JSON deserialization library". This is carried from Bybit and Binance patterns (acceptable), but it blanket-suppresses any new broad catch that might be added without per-site justification in future. Non-blocking: the existing library pattern is consistent across Binance/Bybit.

---

### Finding 4: No test for `GetCandlesticksAsync` — the entire candlestick service path is untested

- **Severity**: MEDIUM
- **Confidence**: 95
- **File**: `tests/CryptoExchanges.Net.Okx.Tests.Unit/OkxMappingAndServiceTests.cs` (absent)
- **Category**: Testing
- **Verdict**: REJECT (paired with Finding 1 — the missing test is the evidence gap for the unguarded parse)

**Issue**: `OkxMarketDataService.GetCandlesticksAsync` is the only service method with zero test coverage. The task manifest lists candlestick service tests as implicitly required ("per-service DeltaMapper mapping"). The gap is especially important because (a) the `long.Parse` bug identified in Finding 1 lives here, and (b) the `MapKlineInterval` switch (8h interval throws) is also untested at the service level. Tests for `GetOrderBook` and `GetRecentTrades` exist, but `GetCandlesticks` was omitted.

**Fix**: Add at minimum:
1. A happy-path test that verifies `OpenTime`, `Open`, `Volume` from a mock 7-element array response.
2. A test that verifies `EightHours` interval throws `ArgumentOutOfRangeException`.
3. A test confirming `limit=500` is clamped to `100` (mirrors the existing `Account_GetTradeHistory_DefaultLimit_ClampsToHundred` pattern).

---

## Summary

| Item | Verdict | Reason |
|------|---------|--------|
| Build (0 warnings, TreatWarningsAsErrors) | PASS | Confirmed via `dotnet build` |
| 91 OKX unit tests pass | PASS | All tests green |
| DeltaMapper mandate: profile + AssertConfigurationIsValid + no AutoMapper | PASS | `OkxClientComposer.CreateMapper` calls it; unit-tested |
| JSON ValueKind guards (OkxErrorTranslator) | PASS | `ReadString` helper guards both `code` and `msg`; object check on `data[0]` |
| Limits clamped (books/candles/trades/history) | PASS | All five clamp sites confirmed |
| 8h bar throws | PASS | Falls to `_ => throw ArgumentOutOfRangeException` |
| CumulativeQuoteQuantity = accFillSz × avgPx | PASS | Mapping + 2 unit tests |
| PriceChangePercent divide-by-zero guard | PASS | Explicit `== 0m` guard in mapping |
| TimeInForce + Type both accept "market" | PASS | Parser tests + regression test |
| Base64 sig vector | PASS | Known-vector test confirmed |
| Prehash GET/POST assembly | PASS | Both forms tested with asserted prehash string |
| Re-sign-on-retry (SeqStub 500→200) | PASS | Integration test, single header set per attempt |
| Passphrase-missing fast-fail | PASS | Integration test throws `InvalidOperationException` |
| Hyphen symbol round-trip | PASS | BTC-USDT → "BTC-USDT" → BTC/USDT |
| Error mapping (code=="0" + per-order sCode) | PASS | All critical cases covered |
| Time-sync + zero-length holder guard | PASS | Both `ApplyOffset` paths tested |
| DI resolution (secretless, passphraseless, scope) | PASS | 8 DI tests |
| MARKET-ORDER regression | PASS | Full round-trip asserted |
| **`long.Parse` on candlestick timestamp (Finding 1)** | **REJECT** | HIGH / confidence 95 — unguarded, throws on malformed wire payload; `ParseMs` exists and is used elsewhere |
| **Missing `GetCandlesticksAsync` test (Finding 4)** | **REJECT** | MEDIUM / confidence 95 — entire candlestick path untested; paired with Finding 1 |
| `TryMapTicker` narrow catch scope (Finding 2) | CONCERN | MEDIUM / confidence 60 — non-blocking |
| `CA1031` global project suppression (Finding 3) | CONCERN | LOW / confidence 55 — consistent with Bybit/Binance pattern |

---

## Final Verdict

**CHANGES_REQUESTED**

Two blocking findings:
1. **Finding 1** (HIGH / 95): `long.Parse(arr[0], ...)` at `OkxMarketDataService.cs:214` — replace with `OkxValueParsers.ParseMs(arr[0])`.
2. **Finding 4** (MEDIUM / 95): `GetCandlesticksAsync` has zero test coverage — add at minimum a happy-path test and a clamping test.

Both are straightforward fixes. All other checklist items pass. The signing, error translation, DeltaMapper profile, limit clamping, DI wiring, and test suite are high quality.
