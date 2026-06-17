# API Review: TASK-003 — BybitSigningHandler

**Commit**: 283bcf0
**Reviewer**: API Reviewer
**Date**: 2026-06-17

**Files under review**:
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs` (new)
- `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs` (doc-cref touch)

**Supporting files read**:
- `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs`
- `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs` (pattern reference)
- `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj`
- `src/CryptoExchanges.Net.Binance/CryptoExchanges.Net.Binance.csproj`

---

### Finding 1: `BybitSigningHandler` and `BybitSignatureService` are `public` but should be `internal`

- **Severity**: HIGH
- **Confidence**: 95
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:13`, `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:13`
- **Category**: API Design / Compatibility
- **Verdict**: REJECT (blocking — confidence 95)
- **Issue**: Both `BybitSigningHandler` and `BybitSignatureService` are declared `public sealed class`. The Bybit `.csproj` already grants `InternalsVisibleTo` to `CryptoExchanges.Net.Bybit.Tests.Integration` and `CryptoExchanges.Net.DependencyInjection` — the only two assemblies that legitimately need to construct these types. The csproj comment reads "wire internal types (IBybitHttpClient, BybitHttpClient, BybitClientComposer, handlers, signing/translator)" — explicitly listing signing as an internal type. Making them `public` locks their constructor signatures as committed API: any future change (e.g., adding a clock abstraction, changing `recvWindow` from `string` to `long`, injecting a `BybitOptions` snapshot) becomes a breaking API change for external callers who instantiated them directly. There is no scenario where a consumer of `CryptoExchanges.Net.Bybit` needs to instantiate `BybitSigningHandler` or `BybitSignatureService` directly.
- **Fix**: Change `public sealed class BybitSigningHandler` to `internal sealed class BybitSigningHandler`. Change `public sealed class BybitSignatureService` to `internal sealed class BybitSignatureService`. The existing `InternalsVisibleTo` grants in `CryptoExchanges.Net.Bybit.csproj` already cover both legitimate consumers. Note: `BinanceSigningHandler` and `BinanceSignatureService` are also `public` today and share the same problem — that is a pre-existing issue tracked separately; this PR should not worsen it by adding another public signing handler.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/CryptoExchanges.Net.Bybit.csproj:18-21` (InternalsVisibleTo comment: "wire internal types... handlers, signing/translator")

---

### Finding 2: `recvWindow` ctor parameter is `string` — pre-formatted string is a weak public contract

- **Severity**: MEDIUM
- **Confidence**: 80
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:13-14`
- **Category**: API Design
- **Verdict**: REJECT (blocking — confidence 80; conditioned on Finding 1: if type becomes `internal` this downgrades to CONCERN/non-blocking)
- **Issue**: The constructor accepts `string recvWindow`. While the TASK-003 manifest justifies this as "keeps the handler agnostic of BybitOptions" — that reasoning is sound for an internal type — as a `public` contract it provides zero enforcement. A caller who passes a locale-formatted value or whitespace gets a subtly broken `X-BAPI-RECV-WINDOW` header with no compile-time feedback. Additionally, the ctor parameter order `(string apiKey, BybitSignatureService signatureService, string recvWindow, Func<long> timeOffset)` places `recvWindow` between the signature service and the time offset, which is not logically grouped. Binance's ctor is `(string apiKey, BinanceSignatureService signatureService, Func<long> timeOffset)` — tight and positionally clear.
- **Fix**: If the type remains `public` (not recommended per Finding 1): change `string recvWindow` to `int recvWindow` (or `long`) and format internally at point of use (`recvWindow.ToString(CultureInfo.InvariantCulture)`). Move `timeOffset` to last position for parity with Binance: `(string apiKey, BybitSignatureService signatureService, int recvWindow, Func<long> timeOffset)`. If the type is correctly made `internal` (Finding 1 fix accepted): the string form is acceptable since the single internal call site in the DI composer controls formatting; adding `ArgumentException.ThrowIfNullOrWhiteSpace(recvWindow)` in the constructor is still good practice.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs:12-13` (Binance ctor: no pre-formatted strings for config values)

---

### Finding 3: Missing `<param>` XML docs on all four ctor parameters of `BybitSigningHandler`

- **Severity**: LOW
- **Confidence**: 90
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:6-15`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence 90)
- **Issue**: The class-level `<summary>` is thorough and accurate, but primary-constructor parameters `apiKey`, `signatureService`, `recvWindow`, and `timeOffset` have no `<param>` documentation. The csproj suppresses `CS1591` so the build will not warn. If the class remains public, full param docs are expected. If made `internal` (Finding 1 fix), this is minor.
- **Fix**: Add `<param name="apiKey">...</param>`, `<param name="signatureService">...</param>`, `<param name="recvWindow">...</param>`, `<param name="timeOffset">...</param>` to the class XML doc block.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:29-44` (param tags on static methods show the expected doc depth)

---

### Finding 4: `BybitSigningRequest` public visibility is misleading but consistent with Binance parity

- **Severity**: LOW
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningRequest.cs:5-21`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence 70)
- **Issue**: `BybitSigningRequest` is `public static class` with two `public static` methods. This is consistent with `BinanceSigningRequest` (also public), so it is not a regression. However, `MarkSigned` is only useful to code that constructs `HttpRequestMessage` objects and injects them into Bybit's HTTP pipeline — exclusively internal pipeline code. An external consumer calling `MarkSigned` on an arbitrary `HttpRequestMessage` achieves nothing because the message must flow through `BybitSigningHandler` inside the Bybit HTTP pipeline. The public surface is misleading. Deferred given Binance parity.
- **Fix**: No immediate action required. Track as a cleanup item: make `BybitSigningRequest` (and `BinanceSigningRequest`) `internal` in a future minor revision, consistent with the InternalsVisibleTo model already in place.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningRequest.cs:5` (same public pattern being mirrored)

---

### Finding 5: Guard asymmetry in `BuildGetSignString` / `BuildPostSignString`

- **Severity**: LOW
- **Confidence**: 85
- **File**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:37-61`
- **Category**: API Design
- **Verdict**: CONCERN (non-blocking — confidence 85; moot if type becomes `internal`)
- **Issue**: `queryString`/`jsonBody` are guarded with `ArgumentNullException.ThrowIfNull` while `timestamp`/`apiKey`/`recvWindow` use `ArgumentException.ThrowIfNullOrWhiteSpace`. An empty `queryString` is valid (GET with no query params) so that guard level is correct. An empty `jsonBody` is a minor semantic inconsistency — functionally a non-issue since the signing handler only reaches `BuildPostSignString` when `request.Content is not null` and `ReadAsStringAsync` has returned content.
- **Fix**: No change needed if the type becomes `internal`. If it stays `public`, add an inline comment explaining that empty `jsonBody` is valid for parameter-less POST endpoints.
- **Pattern reference**: `src/CryptoExchanges.Net.Bybit/Auth/BybitSignatureService.cs:39-41`

---

### Summary

- REJECT: `BybitSigningHandler` and `BybitSignatureService` are `public` — should be `internal`; csproj InternalsVisibleTo grants already cover both legitimate consumers; public constructors lock signing internals as committed API (confidence: 95/100, blocking)
- REJECT: `recvWindow` ctor param is `string` — if class remains public, this is an enforcement gap; fix by accepting `int`/`long` and formatting internally; conditioned on Finding 1: becomes non-blocking if type is made `internal` (confidence: 80/100, blocking only if public modifier retained)
- CONCERN: Missing `<param>` XML docs on all four ctor parameters of `BybitSigningHandler` (confidence: 90/100, non-blocking)
- CONCERN: `BybitSigningRequest` public visibility is misleading but mirrors Binance exactly; track for future symmetric cleanup (confidence: 70/100, non-blocking)
- CONCERN: Guard asymmetry in `BuildGetSignString`/`BuildPostSignString`; moot at `internal` visibility (confidence: 85/100, non-blocking)
- PASS: `sealed class` and primary-ctor shape match `BinanceSigningHandler` exactly
- PASS: `DelegatingHandler` inheritance, `ConfigureAwait(false)` throughout — correct
- PASS: Header strip-before-re-add pattern (`Remove` then `Add`) correctly prevents doubling on retry — satisfies acceptance criterion 2
- PASS: Unsigned requests receive only the api-key header, no signing headers — satisfies acceptance criterion 3
- PASS: GET vs POST branching logic mirrors Binance structure; Bybit-specific body-read-without-replace deviation is correct and justified
- PASS: `BybitSigningRequest` doc-cref upgrade is accurate and non-breaking
- PASS: No new `InternalsVisibleTo` grants added in this task; existing grants are correctly scoped

---

## Final Verdict

**CHANGES_REQUESTED**

Both blocking findings stem from the same root cause: `BybitSigningHandler` and `BybitSignatureService` are `public` when the project's InternalsVisibleTo model, the csproj comment, and the intended encapsulation all indicate they are infrastructure-internal types. Changing both to `internal sealed class` resolves Finding 1 outright and automatically downgrades Finding 2 from blocking to a non-blocking style note. All other concerns are non-blocking and can be addressed in the same pass.
