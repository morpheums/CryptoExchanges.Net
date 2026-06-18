# Security Review — TASK-023 Group B (PR #16 cross-cutting fixes #1/#5/#6)

Commits reviewed: `524e017` (Group A) and `0bee170` (Group B), diff from `c5027e4` to `HEAD`.

---

### Finding 1: `ExchangeUrl.BuildQueryString` — byte-identical to every replaced per-exchange implementation

- **Severity**: LOW (would be HIGH if divergent; it is not)
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/ExchangeUrl.cs:19-29`
- **Category**: Signing integrity
- **Verdict**: PASS

The shared implementation is structurally identical to all four removed per-exchange copies (verified line-by-line in diff):
- Null/empty short-circuit returns `string.Empty`
- Iterates in insertion order (`Dictionary<string,string>` preserves insertion order in .NET 5+)
- Both key and value go through `Uri.EscapeDataString`
- Separator is `&` appended before each subsequent pair (not after), producing no trailing `&`
- Uses `StringBuilder` with identical Append chain

The only difference is the parameter type is widened from `Dictionary<string, string>?` to `IReadOnlyDictionary<string, string>?`. All call sites still pass `Dictionary<string, string>`, which implements `IReadOnlyDictionary<string, string>`, so the actual runtime dictionary type (and thus its iteration order) is unchanged. No behavioral difference.

Unit test `BuildQueryString_JoinsAndEscapesInOrder` in `ExchangeUrlTests.cs` directly asserts the escape and join behavior. All 21 Http-layer unit tests pass.

---

### Finding 2: `ExchangeUrl.NormalizeHostRoot` — strengthens OKX signing, Binance and Bybit correctly excluded

- **Severity**: LOW (noting the intentional scope boundary)
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Http/ExchangeUrl.cs:38-48`
- **Category**: Signing integrity
- **Verdict**: PASS

**OKX before this change** used `options.BaseUrl.TrimEnd('/')` in `OkxClientComposer` and `o.BaseUrl` bare in `OkxServiceCollectionExtensions` — the DI path had no path-segment guard at all. This change adds `NormalizeHostRoot` on both the container-free and DI paths for OKX, closing the gap.

**Why OKX and Bitget require it but Binance and Bybit do not**: OKX signs `RequestUri.PathAndQuery`; Bitget signs `RequestUri.AbsolutePath` + `RequestUri.Query` separately. Both prehash computations would be corrupted if the HttpClient's BaseAddress carried a path segment, because `RequestUri.AbsolutePath` would then include the base path as a prefix. Binance's signing handler reconstructs the URI from `uri.GetLeftPart(UriPartial.Path)` / `uri.Query` and rewrites the full URI string — it never relies on `AbsolutePath` as the signed component. Bybit's handler extracts only `RequestUri.Query` for the sign-string body; the path is not part of the Bybit prehash at all. Therefore `NormalizeHostRoot` is not required for signing correctness on Binance/Bybit, and the decision not to add it there is justified.

The shared implementation is semantically identical to the removed `BitgetClientComposer.NormalizeHostRoot`: same `ThrowIfNullOrWhiteSpace`, same `new Uri(baseUrl, UriKind.Absolute)`, same `AbsolutePath is not ("/" or "")` check, same exception message pattern, same `TrimEnd('/')` return. The error message was slightly generalized — correct since it is now shared.

`NormalizeHostRoot_WithPathSegment_Throws` and `NormalizeHostRoot_HostOnly_TrimsTrailingSlash` unit tests exercise both paths. The new OKX DI test `Di_AddOkxExchange_BaseUrlWithPath_FailFast` confirms the guard fires on the DI-wired path.

---

### Finding 3: `SyncServerTimeAsync` skip-on-zero — stale offset analysis

- **Severity**: LOW
- **Confidence**: 95
- **File**: All four exchange clients — `BinanceExchangeClient.cs:108-112`, `BybitExchangeClient.cs:650-651`, `OkxExchangeClient.cs:782-783`, `BitgetExchangeClient.cs:449-451`
- **Category**: Signing integrity
- **Verdict**: PASS

The behavior is: if `serverTimeMs <= 0`, skip the `ApplyOffset` call and keep the current value of `offsetHolder[0]`.

There are three cases:

1. **Client never called `SyncServerTimeAsync`**: `offsetHolder[0]` is `0L` (initialized at construction). This is the pre-existing behavior and is unchanged — offset 0 means timestamp = local clock, which is correct for a well-synchronized local system.

2. **`SyncServerTimeAsync` previously succeeded, then returns a zero response**: `offsetHolder[0]` retains the last good offset, which is strictly better than either (a) corrupting it to `serverTime=0 - localNow ≈ -now_epoch` (which would have been the old behavior before the fix), or (b) throwing and unwinding the caller's sync loop.

3. **All calls return zero**: same as case 1.

The defense-in-depth is preserved: `ExchangeTimeSync.ApplyOffset` still throws `ArgumentOutOfRangeException` if called with `serverTimeMs <= 0`. The per-client skip guard means that protection is never bypassed — the skip itself prevents reaching `ApplyOffset` with a bad value. This is sound: skip-and-keep-prior is strictly safer than either throw-and-propagate or write-corrupt-offset.

---

### Finding 4: `ExchangeUrl` is `internal` — no public serialization surface introduced

- **Severity**: LOW (non-issue)
- **Confidence**: 100
- **File**: `src/CryptoExchanges.Net.Http/ExchangeUrl.cs:12`
- **Category**: Credential safety
- **Verdict**: PASS

`ExchangeUrl` is declared `internal static`. It carries no state (no fields), takes no secret parameters, and has no `ToString()` or serialization path. The `InternalsVisibleTo` for the test project (`CryptoExchanges.Net.Http.Tests.Unit`) was correctly added to the csproj so tests can reach the internal class.

---

### Finding 5: No new secret exposure vectors in the diff

- **Severity**: N/A
- **Confidence**: 99
- **Verdict**: PASS

Grep over the full diff for `SecretKey`, `ApiKey` (beyond existing field declarations), `JsonSerializer` applied to options, `ToString` on any options type, and `log`/`Log` calls: none of the changed files introduce new exposure. The `ExchangeUrl.cs` `StringBuilder.ToString()` is the only `ToString` hit and it operates on the query string being assembled, not credentials.

No new `[JsonInclude]` attributes, no new `ToString()` overrides on options classes, no new places where `SecretKey` is passed to anything other than the existing signing handler constructors.

---

### Finding 6: Bitget order-book bounds guard (#2)

- **Severity**: LOW
- **Confidence**: 97
- **File**: `src/CryptoExchanges.Net.Bitget/Services/BitgetMarketDataService.cs:194-196`
- **Category**: Input validation (robustness)
- **Verdict**: PASS

The `Where(b => b.Count >= 2)` filter is the correct fix. `List<string>` length guard before index access is safe. The new test `MarketData_GetOrderBook_SkipsShortLevels` covers both `Bids` and `Asks` with `[]` and `["99"]` (single-element) malformed rows and asserts only the valid rows survive.

---

### Finding 7: Binance and Bybit DI/composer paths do not adopt `NormalizeHostRoot` — intentional, but inconsistent with the new hardening pattern

- **Severity**: LOW
- **Confidence**: 75
- **File**: `src/CryptoExchanges.Net.Binance/ServiceCollectionExtensions.cs:48`, `src/CryptoExchanges.Net.Bybit/ServiceCollectionExtensions.cs:48`
- **Category**: Signing integrity
- **Verdict**: CONCERN (non-blocking, confidence 75)
- **Issue**: Binance and Bybit DI registrations still use bare `o.BaseUrl`. As established in Finding 2, Binance/Bybit signing does not use `AbsolutePath` as a signed component, so this is not a correctness issue today. However, a misconfigured base URL with a path segment would silently produce broken requests rather than failing fast.
- **Fix**: Apply `ExchangeUrl.NormalizeHostRoot(o.BaseUrl)` on the Binance and Bybit DI `baseUrlSelector` and their respective `ClientComposer.BuildResilientHttpClient` for consistency and defense-in-depth, matching the pattern now established on OKX and Bitget.
- **Pattern reference**: `src/CryptoExchanges.Net.Okx/ServiceCollectionExtensions.cs:52`

---

### Summary

- PASS: `ExchangeUrl.BuildQueryString` — byte-identical to all four removed per-exchange copies; both key and value are `Uri.EscapeDataString`-escaped, `&`-joined in insertion order; unit-tested
- PASS: `ExchangeUrl.NormalizeHostRoot` — semantically identical to removed `BitgetClientComposer.NormalizeHostRoot`; now enforced on OKX DI path (previously missing); Binance/Bybit correctly excluded because their prehash does not use `AbsolutePath`
- PASS: `SyncServerTimeAsync` skip-on-zero — keeps prior offset rather than corrupting it; `ExchangeTimeSync.ApplyOffset` defense-in-depth remains intact; offset-zero on a never-synced client is the pre-existing baseline behavior
- PASS: `ExchangeUrl` is internal static with no state, no serialization surface, no secret parameters
- PASS: No new secret leakage vectors in any changed file
- PASS: Bitget order-book bounds guard is correct; test covers the malformed-row cases
- CONCERN: Binance and Bybit DI/composer paths do not adopt `NormalizeHostRoot` (confidence: 75, non-blocking) — not a signing correctness issue today since Binance/Bybit prehash does not depend on `AbsolutePath`, but inconsistent with the hardening pattern now applied to OKX and Bitget

---

## Final Verdict

APPROVED

All three focus areas (#6 `BuildQueryString`, #5 `NormalizeHostRoot`, #1 `SyncServerTimeAsync`) are correct. HMAC integrity is preserved across all four exchange signing paths. No secret leakage introduced. The one concern (Binance/Bybit not adopting `NormalizeHostRoot`) is non-blocking at confidence 75 and is a defensive-hardening gap, not a signing vulnerability.
