# Security Review — TASK-006
## Bybit services + DeltaMapper mapping + composer + BybitExchangeClient
**Commit**: 057d6d2
**Reviewer**: Security Reviewer
**Date**: 2026-06-17
**Overall Verdict**: APPROVED
**Confidence**: 95/100

---

## Findings

### Finding: X-BAPI-API-KEY sent on unsigned (market-data) requests from credentialed client
- **Severity**: LOW
- **Confidence**: 72
- **File**: `src/CryptoExchanges.Net.Bybit/Resilience/BybitSigningHandler.cs:26-30`
- **Category**: Security / Credential Exposure
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `BybitSigningHandler.SendAsync` adds `X-BAPI-API-KEY` on every request regardless of whether `IsSigned(request)` is true. This means a credentialed client (one configured with both ApiKey and SecretKey) will include the API-key header on unsigned market-data calls (`/v5/market/tickers`, `/v5/market/orderbook`, `/v5/market/kline`, etc.), even though Bybit does not require it there. The key is not a secret (it authenticates the account, not the operation), and Bybit will silently ignore it on public endpoints, so this is not a cryptographic exposure of `SecretKey`. However, it leaks the API key to any TLS-terminating proxy or log that captures market-data request headers.
- **Fix**: Gate the `X-BAPI-API-KEY` header add on `BybitSigningRequest.IsSigned(request)`, the same condition used for timestamp/signature:
  ```csharp
  if (!string.IsNullOrEmpty(apiKey) && BybitSigningRequest.IsSigned(request))
  {
      request.Headers.Remove("X-BAPI-API-KEY");
      request.Headers.Add("X-BAPI-API-KEY", apiKey);
  }
  ```
  Note: BybitSigningHandler is NOT under review in TASK-006 (it is a pre-existing primitive). This concern is documented so the team can address it in a follow-up hardening task.
- **Pattern reference**: `src/CryptoExchanges.Net.Binance/Internal/BinanceClientComposer.cs:83-84` — Binance adds `X-MBX-APIKEY` as a default HttpClient header (always sent), so the Bybit pattern mirrors that design choice. The concern is rated LOW/non-blocking accordingly.

### Finding: BybitOptions lacks a ToString() override that redacts SecretKey
- **Severity**: LOW
- **Confidence**: 65
- **File**: `src/CryptoExchanges.Net.Bybit/BybitOptions.cs:1-22`
- **Category**: Credential Safety
- **Verdict**: CONCERN (non-blocking — confidence < 80)
- **Issue**: `BybitOptions` has no `ToString()` override. If a caller accidentally logs `options.ToString()` or includes options in a structured log event, the default `object.ToString()` will emit the type name (not the values), so it does not directly expose secrets. However, the codebase baseline pattern for sensitive options classes is to redact secrets in `ToString()` to prevent accidental future exposure. The Binance equivalent (`BinanceOptions`) has the same gap, so this is a pattern-wide concern.
- **Fix**: Add a `ToString()` override that redacts secrets:
  ```csharp
  public override string ToString()
      => $"BybitOptions {{ BaseUrl={BaseUrl}, ApiKey={Redact(ApiKey)}, SecretKey=[REDACTED], TimeoutSeconds={TimeoutSeconds} }}";
  private static string Redact(string value)
      => value.Length <= 4 ? "[REDACTED]" : $"{value[..4]}...";
  ```
- **Pattern reference**: Security checklist item — "Does any new config class need a `ToString()` override that redacts secrets?"

---

## Checklist Results

### Credential Safety
- PASS: `SecretKey` is consumed only inside `BybitSignatureService` (converted to `byte[]` at construction) and is never stored as a string field in any service, composer, or client. `BybitClientComposer.BuildResilientHttpClient` passes it to `new BybitSignatureService(options.SecretKey)` and immediately discards the reference.
- PASS: No `SecretKey` or `ApiKey` appears in exception messages, `RawBody` surfacing, or `ToString()` calls anywhere in the six reviewed files.
- PASS: `BybitOptions` has no `[JsonInclude]`, `[DataMember]`, or `[Serializable]` attributes. Neither `ApiKey` nor `SecretKey` appears in any serialization path in the reviewed code.
- PASS: `SecretKey` is never transmitted as a header or query parameter. It is used exclusively to seed `HMACSHA256.HashData` inside `BybitSignatureService.Sign`; the wire output is only the hex signature.
- PASS: `CreateFromEnvironment()` reads `BYBIT_API_KEY` / `BYBIT_SECRET_KEY` safely via `Environment.GetEnvironmentVariable` with `?? string.Empty` fallback. This mirrors the Binance pattern at `BinanceExchangeClient.cs:93-94` and is the approved alternative credential source.

### Signed vs Unsigned Endpoint Classification
All HTTP calls verified — every `signed` boolean is correct:

**Market data (all unsigned = `false`):**
- `/v5/market/tickers` — `GetAsync(..., false)` (x2, lines 169, 265)
- `/v5/market/orderbook` — `GetAsync(..., false)` (line 195)
- `/v5/market/kline` — `GetAsync(..., false)` (line 229)
- `/v5/market/recent-trade` — `GetAsync(..., false)` (line 280)
- `/v5/market/instruments-info` — `GetAsync(..., false)` (line 299)
- `/v5/market/time` (SyncServerTimeAsync + PingAsync) — `GetAsync(..., signed: false)` (BybitExchangeClient.cs:85, 97)

**Trading (all signed = `true`):**
- `/v5/order/create` — `PostAsync(..., true)` (line 123)
- `/v5/order/cancel` (by orderId) — `PostAsync(..., true)` (line 139)
- `/v5/order/cancel` (by clientOrderId) — `PostAsync(..., true)` (line 155)
- `/v5/order/cancel-all` — `PostAsync(..., true)` (line 171)
- `/v5/order/realtime` (GetOpenOrders) — `GetAsync(..., true)` (line 192)
- `/v5/order/history` — `GetAsync(..., true)` (line 219)
- `/v5/order/realtime` (FetchOrderAsync) — `GetAsync(..., true)` (line 240)
- `/v5/order/history` (FetchOrderAsync) — `GetAsync(..., true)` (line 245)

**Account (all signed = `true`):**
- `/v5/account/wallet-balance` (GetBalanceAsync) — `GetAsync(..., true)` (line 96)
- `/v5/execution/list` — `GetAsync(..., true)` (line 128)
- `/v5/account/wallet-balance` (FetchCoinBalancesAsync) — `GetAsync(..., true)` (line 148)

No misclassification found. Zero unsigned-when-should-be-signed or signed-when-should-be-unsigned endpoints.

### Secret-Gated Finalizer (PassThrough Pattern)
- PASS: `BuildResilientHttpClient` at `BybitClientComposer.cs:84-90` correctly gates the finalizer: `string.IsNullOrEmpty(options.SecretKey) ? new PassThroughHandler() : new BybitSigningHandler(...)`. A secretless client receives a no-op `PassThroughHandler`; market-data works credential-less. A credentialed client always has a `BybitSigningHandler` that signs requests marked via `BybitSigningRequest.MarkSigned`.
- PASS: `BybitSigningHandler` is only instantiated when `SecretKey` is non-empty, so a public client can never accidentally sign.

### Signing Integrity — Mark-and-Strip Pattern
- PASS: `BybitHttpClient.GetAsync/PostAsync/DeleteAsync` call `BybitSigningRequest.MarkSigned(request)` before `SendAsync` iff `signed == true`. The mark propagates via `HttpRequestMessage.Options`, which survive the retry boundary (options are per-message, not per-attempt transport state).
- PASS: `BybitSigningHandler.ResignAsync` removes `X-BAPI-TIMESTAMP`, `X-BAPI-RECV-WINDOW`, and `X-BAPI-SIGN` before adding fresh values on every attempt (lines 59-64). This is the correct mark-and-strip pattern preventing duplicate auth headers on Polly retries.
- PASS: `X-BAPI-API-KEY` is also stripped (`Headers.Remove`) before re-adding (line 28-29), preventing doubling on retry.
- PASS: `BybitSigningHandler` sits below the Polly retry boundary (it is the `requestFinalizer` passed to `HttpClientPipelineBuilder.Build`), consistent with Binance's `BinanceSigningHandler` placement.

### Query String Safety
- PASS: `BybitHttpClient.BuildQueryString` (lines 71-81) applies `Uri.EscapeDataString()` to both key and value for every parameter. Reference: `BinanceHttpClient.cs:91`.
- PASS: POST bodies are `JsonSerializer.Serialize(parameters)` — JSON serialization is injection-safe by construction (string values are JSON-escaped).
- PASS: No URL construction by raw string concatenation of user-supplied values found in any of the six reviewed files.

### Input Validation
- PASS: `BybitHttpClient` validates `endpoint` at the method boundary with `ArgumentException.ThrowIfNullOrWhiteSpace(endpoint)`.
- PASS: `PlaceOrderRequest.Validate()` is called on line 91 of `BybitTradingService.PlaceOrderAsync` before any HTTP call.
- PASS: `BybitRequestValidation.ValidateHistoryWindow` is called in `GetOrderHistoryAsync` (trading) and `GetTradeHistoryAsync` (account) before building query parameters.
- PASS: `BybitValueParsers.ParseAssetOrNone` returns `Asset.None` for unrecognized coin tickers rather than throwing, correctly handling `Asset.None` gracefully in the `BybitCoinBalance -> AssetBalance` mapping.
- PASS: `BybitClientComposer.BuildResilientHttpClient` validates `options` and `offsetHolder` with `ArgumentNullException.ThrowIfNull`.

### Time-Sync / Thread Safety
- PASS: `SyncServerTimeAsync` calls `/v5/market/time` with `signed: false` — no credentials needed for a public time endpoint.
- PASS: `offsetHolder[0]` is written by `SyncServerTimeAsync` via `Interlocked.Exchange` and read by the signing handler closure via `Interlocked.Read`. Both operate on `ref offsetHolder[0]`, which is a valid 64-bit aligned array element. This is the same pattern used in `BinanceExchangeClient.cs:43-47` and is thread-safe on all .NET-supported platforms.
- PASS: No auth-bypass risk from the offset: a corrupted/zero offset shifts the timestamp but does not alter the HMAC key or allow a caller to sign with a different key.

### recvWindow Formatting
- PASS: `options.ReceiveWindow.ToString(CultureInfo.InvariantCulture)` at `BybitClientComposer.cs:83` produces a culture-invariant decimal string (e.g., `"5000"` not `"5.000"` on a European locale). The `decimal` type with `InvariantCulture` will never produce a thousands separator or localized decimal point.

### Rate Limiting
- PASS: `BuildResilientHttpClient` registers `ReactiveRateLimitGate` as the gate argument to `HttpClientPipelineBuilder.Build`.
- PASS: `BybitErrorTranslator` correctly classifies HTTP 429, retCode 10006, and retCode 10018 as `RateLimitExceededException`.

### JSON Deserialization Safety
- PASS: `BybitErrorTranslator.Parse` wraps `JsonDocument.Parse(body)` in a try/catch for `JsonException` (lines 57-65). Malformed JSON produces `(null, null)` rather than an unhandled exception.
- PASS: `BybitHttpClient` uses `ReadFromJsonAsync<T>` on responses that have already passed through the resilience pipeline's error translator, which throws typed exceptions on non-2xx status or non-zero retCode. A plain-text error page at the HTTP layer would be caught by the error translator before deserialization is attempted.

---

## Summary

- PASS: Credential safety — `SecretKey` never leaves the `BybitSignatureService`; no logging, serialization, or query-string exposure.
- PASS: `CreateFromEnvironment()` reads `BYBIT_API_KEY`/`BYBIT_SECRET_KEY` env vars safely, mirroring the approved Binance pattern.
- PASS: Signed/unsigned classification — all 16 HTTP calls correctly classified: market-data unsigned, trading/account signed.
- PASS: Secret-gated `PassThroughHandler` finalizer — secretless client is genuinely incapable of signing; credentialed client always signs signed calls.
- PASS: Mark-and-strip signing integrity — `BybitSigningRequest.MarkSigned` + `ResignAsync` header-strip on every attempt prevents duplicate auth headers on Polly retries.
- PASS: `Uri.EscapeDataString` on all query string values; JSON-safe POST bodies.
- PASS: `PlaceOrderRequest.Validate()` called before every order placement.
- PASS: `Interlocked.Read`/`Exchange` thread-safe offset sharing; `/v5/market/time` called unsigned.
- PASS: `ReceiveWindow` formatted with `CultureInfo.InvariantCulture`.
- PASS: `ReactiveRateLimitGate` registered; `BybitErrorTranslator` maps 429/10006/10018 to `RateLimitExceededException`.
- PASS: `JsonException` handled in error translator; `ReadFromJsonAsync` protected by upstream error translation.
- CONCERN: `X-BAPI-API-KEY` header added on unsigned market-data requests from credentialed clients — confidence 72/100, non-blocking. Mirrors the Binance `X-MBX-APIKEY` default-header design; LOW severity.
- CONCERN: `BybitOptions` has no `ToString()` override redacting `SecretKey`/`ApiKey` — confidence 65/100, non-blocking. No `object.ToString()` leaks values today; hardening recommendation only.

## Final Verdict

**APPROVED** — Confidence 95/100

No blocking findings. Both concerns are below the 80-confidence threshold and are LOW severity. The signed/unsigned classification is fully correct across all 16 HTTP calls. Secret handling is sound: `SecretKey` stays in `BybitSignatureService._secretKeyBytes`, never surfaces in query strings, headers, logs, or exceptions. The secret-gated `PassThroughHandler` pattern correctly prevents credential-less clients from signing. The mark-and-strip retry pattern is implemented correctly in `BybitSigningHandler`.
