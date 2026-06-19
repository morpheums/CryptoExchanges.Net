# API Review — TASK-042

## Verdict: APPROVED

## Score: 96

## Summary
The new streaming public surface is clean, minimal, and follows established patterns exactly. No existing REST interfaces are modified. All subscribe methods, naming conventions, CancellationToken placement, return types, and IAsyncDisposable inheritance are correct.

## Findings

### PASS — REST interface surface untouched (Confidence: 100%)
`IExchangeClient`, `IMarketDataService`, `ITradingService`, `IAccountService`, and `IExchangeClientFactory` are not modified in this diff. The diff adds only new files under `Streaming/` and `Interfaces/`. REST surface is fully preserved.

### PASS — IStreamClient : IAsyncDisposable (Confidence: 100%)
`IStreamClient` inherits `IAsyncDisposable` at `IStreamClient.cs:13`. Pattern matches `IExchangeClient : IAsyncDisposable` at `IExchangeClient.cs:10`.

### PASS — IStreamSubscription : IAsyncDisposable (Confidence: 100%)
`IStreamSubscription` inherits `IAsyncDisposable` at `IStreamSubscription.cs:8`. Correct.

### PASS — Naming conventions: SubscribeTo[Type]Async (Confidence: 100%)
All four methods are present and correctly named: `SubscribeToTickerAsync`, `SubscribeToTradesAsync`, `SubscribeToOrderBookAsync`, `SubscribeToKlinesAsync` at `IStreamClient.cs:28,42,60,77`.

### PASS — CancellationToken ct = default as last parameter (Confidence: 100%)
All four subscribe methods end with `CancellationToken ct = default`. Pattern is consistent with `IExchangeClient.cs:25`.

### PASS — Return type Task<IStreamSubscription> (Confidence: 100%)
All four subscribe methods return `Task<IStreamSubscription>`, not `ValueTask` and not a concrete type. Correct.

### PASS — TryGet pattern mirrors IExchangeClientFactory (Confidence: 100%)
`IStreamClientFactory.TryGet(ExchangeId exchange, [NotNullWhen(true)] out IStreamClient? client)` at `IStreamClientFactory.cs:35` exactly mirrors `IExchangeClientFactory.TryGet(ExchangeId exchange, [NotNullWhen(true)] out IExchangeClient? client)` at `IExchangeClientFactory.cs:26`. The `[NotNullWhen(true)]` attribute, parameter names, and nullability annotation are all correct.

### PASS — No order-book maintenance hook (Confidence: 100%)
No `IOrderBookMaintainer`, snapshot-apply hook, or any "reserved for future" interface member is present in this diff. The order-book subscription delivers `OrderBook` per-frame snapshots exactly as scoped: the `LastUpdateId` sequence field is documented for consumer-side gap detection and the maintenance hook is explicitly deferred to a future separate interface.

### PASS — No placeholder or Obsolete stubs (Confidence: 100%)
No `[Obsolete]` attributes, `// TODO`, `// reserved`, or "v1.1" markers anywhere in the diff.

### PASS — No competitor exchange names in committed files (Confidence: 100%)
No third-party exchange names appear in any of the new source files.

### PASS — Clean, minimal surface (Confidence: 97%)
`IStreamClient` exposes exactly four subscribe methods plus `ExchangeId`. `IStreamSubscription` exposes `State` and `IsConnected` (a correct and useful convenience property — confirmed by the theory test at `StreamHandlersTests.cs:69-79`). `StreamHandlers<T>` bundles `OnUpdate` (required) plus three optional lifecycle callbacks. `StreamLag` carries `DroppedCount`. No members appear speculative or prematurely extended.

### CONCERN — IsConnected is an abstract interface property, not a DIM (Confidence: 55%, non-blocking)
`IStreamSubscription.IsConnected` at `IStreamSubscription.cs:26` is declared as an abstract property. Because it is derivable from `State`, it could be expressed as a default interface method (`bool IsConnected => State == StreamConnectionState.Live;`), which would relieve implementers of the obligation to spell it out. The test `FakeSubscription` at `StreamHandlersTests.cs:86` manually implements it. This is a mild implementer-burden point, but the property is simple to implement and its contract is unambiguous, so this is non-blocking. If the team prefers to reduce implementer surface, convert it to a DIM.

### Rule references
LR-004: not applicable — no array parameters exist on any type in this diff. No indexed array access is present.

