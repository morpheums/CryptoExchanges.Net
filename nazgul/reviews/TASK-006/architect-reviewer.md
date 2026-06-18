# Architect Review — TASK-006
**Overall Verdict**: APPROVED  
**Confidence**: 88/100  
**Reviewed commit**: 057d6d2  
**Files under review**: BybitMarketDataService.cs, BybitTradingService.cs, BybitAccountService.cs, BybitMappingProfiles.cs, BybitClientComposer.cs, BybitExchangeClient.cs

---

## Findings

### Finding 1: Layer integrity — no violations
- **Severity**: PASS
- **Confidence**: 99
- **File**: `CryptoExchanges.Net.Bybit.csproj:12-14`
- **Issue**: None. `<ProjectReference>` nodes are Core + Http only. No DI or Binance references.
- **Fix**: N/A
- **Pattern reference**: `CryptoExchanges.Net.Binance.csproj`

---

### Finding 2: Visibility — no internals leak
- **Severity**: PASS
- **Confidence**: 99
- **File**: All six files
- **Issue**: None. Only `BybitExchangeClient` and `BybitOptions` are public among the created files. `BybitResponseProfile`, `BybitClientComposer`, all three services, all DTOs, and `IBybitHttpClient` are `internal`. `InternalsVisibleTo` grants match the Binance pattern (test + DI assemblies only).

---

### Finding 3: Interface contract — no additions to Core interfaces
- **Severity**: PASS
- **Confidence**: 99
- **File**: `IExchangeClient.cs`, `IMarketDataService.cs`, `ITradingService.cs`, `IAccountService.cs`
- **Issue**: None. No interface definitions were modified. The three services fully implement their respective interfaces.

---

### Finding 4: Composer pattern parity with BinanceClientComposer
- **Severity**: PASS
- **Confidence**: 97
- **File**: `Internal/BybitClientComposer.cs:1-99`
- **Issue**: None. All five methods present: `CreateMapper`, `Create`, `ComposeOver`, `ComposeWith`, `ComposeForDi`, `BuildResilientHttpClient`. Signatures, argument names, and `ownsHttpClient` semantics match exactly. `ComposeForDi` carries the `// INVARIANT: ownsHttpClient MUST stay false` comment. Keyed service resolution uses `ExchangeId.Bybit`.

---

### Finding 5: ownsHttpClient invariant correctness
- **Severity**: PASS
- **Confidence**: 99
- **File**: `Internal/BybitClientComposer.cs:27-34` (Create path), `Internal/BybitClientComposer.cs:58-66` (DI path)
- **Issue**: None. `Create` → `ownsHttpClient: true`, passes the `HttpClient` reference. `ComposeForDi` → `ownsHttpClient: false`, passes `httpClient: null`. `DisposeAsync` gates on `_ownsHttpClient && _httpClient is not null`, so a factory-owned client is never disposed here. No double-dispose risk.

---

### Finding 6: Secret-gated finalizer — Create vs DI path consistency
- **Severity**: PASS
- **Confidence**: 97
- **File**: `Internal/BybitClientComposer.cs:84-90`
- **Issue**: None. The `BuildResilientHttpClient` method uses an explicit `PassThroughHandler` when `SecretKey` is empty, not `null`. This is a documented deviation from Binance (which passes `null`), justified by the task notes — both are functionally equivalent (`HttpClientPipelineBuilder.Build` accepts `null` or a no-op handler), but the explicit `PassThroughHandler` is cleaner and consistent with the DI resolution-time gate.

---

### Finding 7: Time-sync design — offsetHolder sharing
- **Severity**: PASS
- **Confidence**: 99
- **File**: `Internal/BybitClientComposer.cs:30`, `BybitExchangeClient.cs:87-89`, `Resilience/BybitSigningHandler.cs:40`
- **Issue**: None. Single `long[] { 0L }` allocated in `Create`, passed into both `BuildResilientHttpClient` (closure in `BybitSigningHandler`) and the client constructor (used in `SyncServerTimeAsync`). The signing handler reads with `timeOffset()` = `Interlocked.Read(ref offsetHolder[0])`. `SyncServerTimeAsync` writes with `Interlocked.Exchange(ref _offsetHolder[0], offset)`. Both operations are interlocked; the design matches the Binance pattern exactly.

---

### Finding 8: DeltaMapper profile correctness
- **Severity**: PASS
- **Confidence**: 97
- **File**: `Mapping/BybitMappingProfiles.cs:1-89`
- **Issue**: None. Four maps defined: `BybitOrder→Order`, `BybitTicker→Ticker`, `BybitInstrument→SymbolInfo`, `BybitCoinBalance→AssetBalance`. All fields either mapped or explicitly `.Ignore()`d. `AssertConfigurationIsValid()` called in `CreateMapper`. `BybitResponseProfile` extends `Profile` from DeltaMapper, not AutoMapper. `ISymbolMapper` passed via constructor, not via static access. Pattern matches `BinanceResponseProfile` faithfully.

---

### Finding 9: Lazy<Task<>> supported-symbols cache pattern
- **Severity**: PASS
- **Confidence**: 95
- **File**: `Services/BybitMarketDataService.cs:140-160`
- **Issue**: None. The `Lazy<Task<>>` field is initialized lazily under a lock with double-checked null guard. `LazyThreadSafetyMode.ExecutionAndPublication` ensures only one fetch runs. The resulting `Task` is shared across concurrent callers. This is appropriate for a read-only, set-once lookup cache. The only non-obvious subtlety — the Lazy holds a Task, so if the first fetch throws, the Lazy caches the faulted Task permanently. This is acceptable for infrastructure (an exchange going offline on first call). Matches Binance's pattern for this scenario.

---

### Finding 10: Retry safety — POST endpoints never retried
- **Severity**: PASS
- **Confidence**: 99
- **File**: `src/CryptoExchanges.Net.Http/ExchangeResiliencePipeline.cs:38-39`
- **Issue**: None. `ShouldHandle` in `ExchangeResiliencePipeline.Configure` gates retry on `req?.Method != HttpMethod.Get` → returns false. All POST calls in `BybitTradingService` (`/v5/order/create`, `/v5/order/cancel`, `/v5/order/cancel-all`) are via `http.PostAsync`, which sets `HttpMethod.Post`, so they are never retried. `FetchOrderAsync` uses `http.GetAsync` → those re-fetch calls are retryable, which is correct (they are reads).

---

### Finding 11: Signing handler — re-sign on retry
- **Severity**: PASS
- **Confidence**: 98
- **File**: `Resilience/BybitSigningHandler.cs:39-65`
- **Issue**: None. Handler strips `X-BAPI-TIMESTAMP`, `X-BAPI-RECV-WINDOW`, `X-BAPI-SIGN` before re-adding them. `IsSigned(request)` drives the resign logic per attempt. This pattern matches Binance's `BinanceSigningHandler` and ensures retried GETs always carry a fresh timestamp within recvWindow.

---

### Finding 12: FetchOrderAsync — hidden double round-trip on PlaceOrder/CancelOrder
- **Severity**: CONCERN
- **Confidence**: 72 (non-blocking)
- **File**: `Services/BybitTradingService.cs:231-253`
- **Category**: Architecture / Contract Fitness
- **Issue**: The `ITradingService` contract's `PlaceOrderAsync` and `CancelOrderAsync` methods return a fully populated `Order`. Bybit V5's `/v5/order/create` and `/v5/order/cancel` return only `{orderId, orderLinkId}`, so a second round-trip to `/v5/order/realtime` (falling back to `/v5/order/history`) is necessary to honour the contract. This is architecturally sound given the V5 constraint — the hidden round-trip is a legitimate consequence of the API shape and does not break any invariant. The risk is that between the first POST (create/cancel succeeds) and the GET (re-fetch), the order may have already transitioned state such that neither `/v5/order/realtime` nor `/v5/order/history` returns it in the query window (extremely unlikely for a freshly created order, possible for cancel on an order that fills immediately). In that case, the last-resort `new Order(mapper.FromWire(wireSymbol), orderId)` returns a minimal record — a deliberate, documented degradation rather than a throw.

  The `CancelOrderByClientIdAsync` path has a subtle edge: `response.Result?.OrderId ?? string.Empty` (line 157). If `Result` is `null` (malformed success response that the error translator missed), `FetchOrderAsync` is called with an empty `orderId`, which will query Bybit with `orderId=` — likely returning nothing — and then return a minimal Order with an empty `OrderId`. This is safe from a crash perspective but the last-resort Order will have an empty `OrderId`, which callers may not expect.

- **Fix (if desired)**: At line 157, fall back to `clientOrderId` (the caller's argument) rather than `string.Empty`, so the last-resort Order record carries the client order ID the caller already knows. Change: `var canceledId = response.Result?.OrderId ?? string.Empty;` to `var canceledId = response.Result?.OrderId ?? string.Empty; // orderId may be empty when ACK was missing; last-resort Order will carry empty id`. Or more explicitly, document that the last-resort `orderId` will be empty when the server acknowledgement is missing.

---

### Finding 13: CancelAllOrdersAsync — MaxHistoryLimit (50) truncation
- **Severity**: CONCERN
- **Confidence**: 70 (non-blocking)
- **File**: `Services/BybitTradingService.cs:176`
- **Category**: Correctness / API Constraint
- **Issue**: `CancelAllOrdersAsync` re-fetches via `GetOrderHistoryAsync(symbol, BybitRequestValidation.MaxHistoryLimit, ...)` = 50 records. If a symbol had more than 50 open orders at the time of cancel-all, not all canceled orders will be present in the re-fetched set, and the returned list will silently omit them. The cancel itself succeeds — only the response is incomplete. This is a known V5 constraint (no single-call bulk fetch of all just-canceled orders) and is documented in the task notes.
- **Fix**: No fix required within this task scope. If complete fidelity is needed in future, consider pagination or returning only the id-list ACKs mapped to minimal Orders when the count exceeds 50.

---

### Finding 14: AssetBalance struct default in GetBalanceAsync
- **Severity**: CONCERN
- **Confidence**: 65 (non-blocking)
- **File**: `Services/BybitAccountService.cs:103`
- **Category**: Correctness
- **Issue**: `match.Asset == asset` works correctly when `FirstOrDefault` returns the default `AssetBalance` (i.e., `Asset = Asset.None`), because `Asset.None != asset` (any valid `asset` has a non-empty Ticker). The fallback `new AssetBalance(asset, 0, 0)` is correctly returned. However, the pattern relies on `AssetBalance` being a value type whose default sentinel (`Asset.None`) is distinguishable from any valid asset — this is implicitly load-bearing. A comment noting this sentinel dependency would improve maintainability.
- **Fix**: Low priority. Add a comment: `// AssetBalance is a value struct; default has Asset.None, which != any valid asset, so this correctly identifies a missing balance.`

---

### Finding 15: Lazy<Task<>> permanent failure caching
- **Severity**: CONCERN
- **Confidence**: 60 (non-blocking)
- **File**: `Services/BybitMarketDataService.cs:151-159`
- **Category**: Resilience
- **Issue**: `Lazy<Task<>>` with `LazyThreadSafetyMode.ExecutionAndPublication` caches a faulted `Task` permanently if `GetExchangeInfoAsync` throws on the first call. Subsequent calls to `IsSupportedAsync`/`ResolveSymbolAsync` will always return the same faulted Task. This is acceptable for the current scope (opt-in validation methods), but callers who retry after a transient failure will always get the original exception. Binance's pattern has the same characteristic.
- **Fix**: No change required. Document this behavior in the XML doc of `IsSupportedAsync` / `ResolveSymbolAsync` if that is a concern.

---

## Summary

- PASS: Layer integrity — Core/Http-only dependencies, no Binance/DI references (confidence: 99)
- PASS: Visibility — only `BybitExchangeClient` + `BybitOptions` public (confidence: 99)
- PASS: No interface additions — all Core interfaces unchanged (confidence: 99)
- PASS: Composer parity — all 5 methods, correct signatures, ownsHttpClient invariant correct (confidence: 97)
- PASS: Secret-gated finalizer — explicit PassThroughHandler when secretless, consistent Create/DI paths (confidence: 97)
- PASS: Time-sync — shared offsetHolder, Interlocked.Read/Exchange pattern, correct closure (confidence: 99)
- PASS: DeltaMapper profile — 4 maps, AssertConfigurationIsValid, DeltaMapper.Profile, bespoke ISymbolMapper (confidence: 97)
- PASS: Lazy<Task<>> cache — double-checked init, ExecutionAndPublication mode, appropriate pattern (confidence: 95)
- PASS: Retry safety — POST never retried, re-fetch GETs correctly retryable (confidence: 99)
- PASS: Signing handler — strips/re-adds headers on retry, per-attempt timestamp (confidence: 98)
- CONCERN: FetchOrderAsync hidden round-trip — acceptable V5 constraint; `CancelOrderByClientIdAsync` may produce empty-OrderId last-resort Order when ACK is null (confidence: 72, non-blocking)
- CONCERN: CancelAllOrdersAsync 50-record truncation — documented V5 limit; cancel succeeds but returned list may be incomplete (confidence: 70, non-blocking)
- CONCERN: AssetBalance default sentinel in GetBalanceAsync — correct but implicitly load-bearing; minor comment hygiene (confidence: 65, non-blocking)
- CONCERN: Lazy<Task<>> permanent failure caching — same as Binance pattern; acceptable, worth documenting (confidence: 60, non-blocking)

## Final Verdict

**APPROVED** — confidence 88/100. No blocking findings. All architectural invariants are upheld: layering is clean, internals are sealed, the composer pattern matches Binance, DeltaMapper is used correctly, time-sync and signing are sound, and retry is GET-only. The four non-blocking concerns are either V5 API constraints with documented rationale or minor code clarity suggestions. Build is clean (0 warnings, 0 errors under TreatWarningsAsErrors=true).
