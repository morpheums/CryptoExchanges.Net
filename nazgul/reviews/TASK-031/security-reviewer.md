# Security Review — TASK-031

## Verdict: APPROVED

## Score: 96/100

## Findings

### Read-Only Scope Enforced — PASS (confidence: 100%)

All six tool methods exclusively call read methods: `GetBalancesAsync`, `GetBalanceAsync`, `GetOpenOrdersAsync`, `GetOrderAsync`, `GetOrderHistoryAsync`, `GetTradeHistoryAsync`. A full scan of the diff and the AccountTools.cs source found zero references to `PlaceOrder`, `CancelOrder`, `CancelAllOrders`, `CancelOrderByClientId`, or any other mutating method on `ITradingService` or `IAccountService`. The constraint holds cleanly.

### AuthenticationException Maps to AuthRequired — PASS (confidence: 100%)

Traced the full chain: `AccountTools → ToolRunner.RunAsync → Categorize(ex)`. The switch expression at `ToolRunner.cs:32` places `AuthenticationException` before `ExchangeApiException` (its base class), so it always routes to `"AuthRequired"`. Two unit tests (`GetBalances_MissingCredentials_MapsToAuthRequired`, `GetOpenOrders_MissingCredentials_MapsToAuthRequired`) assert this mapping. The mapping is correct and tested.

### No Credential Exposure in Error Messages — PASS (confidence: 98%)

`ToolRunner.RunAsync` surfaces `ex.Message`, not `ex.RawBody`. The exception messages constructed by the exchange error translators (e.g., `BinanceErrorTranslator.cs:17`: `"Binance error {code}: {msg}"`) are built from the exchange's own response fields, not from locally stored API key or secret key values. No exchange error translator in the codebase includes an API key or secret in exception message text. `ex.RawBody` is a separate property on `ExchangeApiException` that is not forwarded to `ToolError`.

### No Key Leak Through Exception Messages — PASS (confidence: 98%)

Reviewed all `AuthenticationException` construction sites. Messages take the form `"Binance error -1022: Signature for this request is not valid"` or similar — exchange-supplied text about the signature check outcome, never a reflection of locally held key material. `ToolRunner` does not sanitize exception messages, but the underlying SDK guarantees keys never enter exception message strings.

### IExchangeClientFactory Usage Correct — PASS (confidence: 100%)

`AccountTools` receives `IExchangeClientFactory` via DI injection at the tool method boundary. It uses `factory.TryGet(id, out var client)` exclusively — no direct credential store access, no keyed DI bypass, no `BinanceOptions`/`BybitOptions` etc. referenced. This is the correct factory-mediated access pattern, consistent with `MarketDataTools`.

### No Static Credential State — PASS (confidence: 100%)

`AccountTools` is a `static class` with only two `private const string` fields (`ExchangeParam`, `SymbolParam`) used as parameter description strings. No static fields holding exchange clients, credentials, or mutable state exist. The helper methods `Run<T>` and `Resolve<T>` are stateless dispatch wrappers. There is no per-call state that could leak between requests.

### ToolRunner.ex.Message and RawBody Boundary — NOTE (confidence: 85%, non-blocking)

`ToolRunner.RunAsync` (line 24) constructs `ToolError` with `ex.Message`. For `ExchangeApiException` subclasses (including `AuthenticationException`), `ex.RawBody` contains the raw HTTP response body, which could contain exchange-internal details (order IDs, account identifiers). `RawBody` is correctly excluded from `ToolError`. However, there is no explicit sanitization of `ex.Message` for cases where an exchange might theoretically echo back a credential fragment in its own error response. This is a theoretical risk dependent on exchange behavior, not on SDK code — and the current exception message format from all four translators does not include raw request content.

This is a note for future hardening, not a blocking concern.

### FormatException Category Mismatch for Asset Errors — CONCERN (confidence: 70%, non-blocking)

In `GetBalance` (line 45), a bad asset ticker throws `FormatException("Unknown asset '{asset}'.")`. `ToolRunner.Categorize` maps `FormatException` to `"SymbolNotSupported"`. This is semantically imprecise: a bad asset is not the same as a bad symbol pair. The test `GetBalance_BadAsset_ReturnsError` verifies `Ok == false` but does not assert the error category, so a consumer agent would receive `Category = "SymbolNotSupported"` for bad assets, which is mildly misleading. This does not represent a security issue — no credentials are involved — but it is a minor usability concern at the MCP boundary. Confidence is below 80, so this is non-blocking.

Fix suggestion: introduce a dedicated `AssetNotSupported` category arm in `ToolRunner.Categorize` (alongside a corresponding exception type or a dedicated check), or handle the bad-asset path before entering `ToolRunner` with an early `ToolResult.Failure` return (matching the pattern already used for unknown exchange names and the factory lookup shortcut).

### `limit` Parameter Not Validated — NOTE (confidence: 60%, non-blocking)

`GetOrderHistory` and `GetTradeHistory` accept an unbounded `int limit` with a default of 500. Negative values or extreme values (e.g., `int.MaxValue`) are passed directly to the exchange service layer without clamping. This is not a credential or signing concern; individual exchange services are expected to enforce their own limits and return an `InvalidOrderException` or `ExchangeApiException`, which ToolRunner will catch. Mentioning for completeness as a defensive hardening opportunity.

## Summary

TASK-031 adds six read-only account MCP tools. The security surface is clean: no write methods are exposed, credentials are accessed exclusively through the `IExchangeClientFactory` DI interface, `AuthenticationException` correctly maps to the `"AuthRequired"` error category, and `ToolRunner` never forwards `RawBody` to the agent. No static credential state exists in the `AccountTools` class. The two non-blocking concerns (FormatException category precision for asset errors, and the lack of limit clamping) are below the 80% confidence threshold for rejection and do not affect the security of credentials or the read-only guarantee.
