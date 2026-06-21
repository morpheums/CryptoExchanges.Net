---
reviewer: security-reviewer
task: TASK-059
verdict: APPROVE
---

## Summary

TASK-059 adds the KuCoin REST services, HTTP client, client composer, and entry point. The implementation follows the established OKX/Binance patterns correctly: signing is delegated to `KucoinSigningHandler` (already reviewed and approved in TASK-057), credentials never appear outside the signing layer, `Uri.EscapeDataString` is used for all query string values via `ExchangeUrl.BuildQueryString`, and the Polly retry pipeline is GET-only by construction in `ExchangeResiliencePipeline`. No blocking security issues were found.

---

## Findings

### Finding: `CreateFromEnvironment` silently uses empty strings when env vars are absent
- **Severity**: MEDIUM
- **Confidence**: 70
- **File**: `src/CryptoExchanges.Net.Kucoin/KucoinExchangeClient.cs:76-80`
- **Category**: Secret management / input validation
- **Verdict**: CONCERN (non-blocking — confidence 70, behavior is intentional and documented)
- **Issue**: `CreateFromEnvironment` uses `?? string.Empty` for all three credentials, so if `KUCOIN_API_KEY`, `KUCOIN_SECRET_KEY`, or `KUCOIN_PASSPHRASE` are not set the client is created with empty credentials. The composer's credential-gate (`KucoinClientComposer.BuildResilientHttpClient`, line 115) degrades gracefully to the `PassThroughHandler` when `SecretKey` or `Passphrase` is empty, meaning signed/private calls will be attempted without authentication and will fail with an `AuthenticationException` from the exchange rather than a local `InvalidOperationException`. The Binance and OKX peers follow the same pattern, so this is a conscious design choice — but it differs from the security-focus area's stated preference for an early `InvalidOperationException`.
- **Fix** (non-blocking suggestion): Consider throwing `InvalidOperationException` with a descriptive message when any of the three env vars resolves to empty and no partial-credential usage is intended. Alternatively, document the graceful-degradation intent explicitly on the `CreateFromEnvironment` XML doc.
- **Pattern reference**: `src/CryptoExchanges.Net.Kucoin/Internal/KucoinClientComposer.cs:115` (the PassThrough gate that provides the intentional fallback)

### Finding: `orderId` and `clientOrderId` interpolated directly into URL paths without validation
- **Severity**: LOW
- **Confidence**: 60
- **File**: `src/CryptoExchanges.Net.Kucoin/Services/KucoinTradingService.cs:52,60,139,145`
- **Category**: Input validation / query string safety
- **Verdict**: CONCERN (non-blocking — confidence 60, REST path injection risk is negligible in this context)
- **Issue**: `CancelOrderAsync`, `CancelOrderByClientIdAsync`, and `FetchOrderAsync` construct URL paths by string interpolation of `orderId` / `clientOrderId` (e.g. `$"/api/v1/orders/{orderId}"`). Neither parameter is validated for null/empty/whitespace before use — `CancelOrderAsync(symbol, "")` would send a `DELETE /api/v1/orders/` with an empty tail, which KuCoin would reject with an error but not cause a security breach. In a REST/JSON context with `HttpClient` (which does not interpret `#` or `?` in path segments as meaningful HTTP constructs once appended to a base URI), path traversal/injection risk is negligible. The OKX peer follows the same unescaped path-interpolation pattern. However, an `ArgumentException.ThrowIfNullOrWhiteSpace` guard at the method boundary would be more consistent with the codebase's input-validation approach.
- **Fix** (non-blocking suggestion): Add `ArgumentException.ThrowIfNullOrWhiteSpace(orderId)` at the top of `CancelOrderAsync` and `GetOrderAsync`, and similarly for `clientOrderId` in `CancelOrderByClientIdAsync`, matching the style at every other public method boundary in the project.
- **Pattern reference**: `src/CryptoExchanges.Net.Kucoin/KucoinHttpClient.cs:37` (`ArgumentException.ThrowIfNullOrWhiteSpace(endpoint)` showing the expected guard pattern)

### Finding: `KucoinOptions` has no `ToString()` override redacting secrets
- **Severity**: LOW
- **Confidence**: 50
- **File**: `src/CryptoExchanges.Net.Kucoin/KucoinOptions.cs`
- **Category**: Secret management
- **Verdict**: CONCERN (non-blocking — confidence 50; Binance/OKX peers also lack this override)
- **Issue**: `KucoinOptions` holds `ApiKey`, `SecretKey`, and `Passphrase` as plain `string` properties with no `ToString()` override, so the default object representation would include the field values if an options instance were inadvertently logged or stringified. The security-reviewer checklist calls this out. In practice the Binance peer (`BinanceOptions`) also has no `ToString()` override and the project does not appear to log options objects, so the risk is theoretical. `ExchangeCredentials` (the shared type) does have a redacting `ToString()`, but `KucoinOptions` itself does not delegate to it.
- **Fix** (non-blocking suggestion): Add `public override string ToString() => $"KucoinOptions {{ ApiKey = {(string.IsNullOrEmpty(ApiKey) ? "(empty)" : ApiKey[^Math.Min(4, ApiKey.Length)..]}, SecretKey = [REDACTED], Passphrase = [REDACTED] }}"` for consistency with `ExchangeCredentials.ToString()`, or at minimum a `[DebuggerDisplay]` attribute suppressing secret fields.
- **Pattern reference**: `src/CryptoExchanges.Net.Kucoin/bin/Release/net10.0/CryptoExchanges.Net.Core.xml:49` (`ExchangeCredentials.ToString` — the redacting implementation already in Core)

---

## Security Checks Verified

- **Secrets never logged**: No `ILogger`, `Console.Write*`, or `.ToString()` calls on `ApiKey`, `SecretKey`, or `Passphrase` anywhere in `KucoinHttpClient`, `KucoinExchangeClient`, the three services, or `KucoinClientComposer`. PASS.
- **SecretKey stored only in signing layer**: `SecretKey` is consumed only inside `KucoinSignatureService` (constructed inside `KucoinClientComposer.BuildResilientHttpClient`). `KucoinExchangeClient` holds no reference to it; `KucoinHttpClient` holds no reference to it. PASS.
- **Per-attempt re-sign integrity**: `KucoinHttpClient` calls `KucoinSigningRequest.MarkSigned(request)` on every signed GET, POST, and DELETE. `KucoinSigningHandler.ResignAsync` runs on every attempt and strips/re-sets all five `KC-API-*` headers each time. PASS.
- **Retry-only-on-GET**: `ExchangeResiliencePipeline.Configure` (`ExchangeResiliencePipeline.cs:37`) returns `false` from `ShouldHandle` for any non-GET method. POST (`PlaceOrderAsync`) and DELETE (`CancelOrderAsync`, `CancelAllOrdersAsync`) are never retried. PASS.
- **No opsec leakage**: Comments in new files are strictly technical (exchange API quirks, signing prehash format). No competitive or gateway information. PASS.
- **CreateFromEnvironment credential sourcing**: Reads `KUCOIN_API_KEY`, `KUCOIN_SECRET_KEY`, `KUCOIN_PASSPHRASE` from environment variables only. No hard-coded defaults other than `string.Empty`. CONCERN logged above (non-blocking).
- **Input validation**: `PlaceOrderAsync` calls `request.Validate()` before any HTTP interaction. `KucoinRequestValidation.ValidateHistoryWindow` guards all history endpoints. All public service methods receive strongly-typed inputs (`Symbol`, `Asset`). PASS (with LOW concern on orderId noted above).
- **No credentials in test files**: `KucoinServiceTests.cs` uses NSubstitute mocks of `IKucoinHttpClient`. No `HttpMessageHandler` stubs, no real API keys or secrets anywhere in the test file. PASS.
- **Error messages**: `KucoinErrorTranslator` constructs exception messages as `"KuCoin error {code}: {msg}"` (exchange-provided code + message from the JSON envelope). The raw `body` is attached to `ExchangeApiException.RawBody` only — not embedded in the message string itself. KuCoin error bodies never echo back API keys or secrets. PASS.
- **Query string safety**: All query parameters are routed through `ExchangeUrl.BuildQueryString`, which applies `Uri.EscapeDataString` to both key and value. PASS.
- **KucoinOptions not serializable**: No `[JsonInclude]`, `[JsonProperty]`, or `[Serializable]` attributes. PASS.
- **Rate limiting**: `ReactiveRateLimitGate` is instantiated in `KucoinClientComposer.BuildResilientHttpClient` (line 80) and wired into the pipeline. PASS.
- **429 / rate-limit classification**: `KucoinErrorTranslator` maps `HttpStatusCode.TooManyRequests` to `RateLimitExceededException`. PASS.
- **JSON deserialization safety**: `KucoinErrorTranslator.Parse` wraps `JsonDocument.Parse` in a `try/catch (JsonException)`. `KucoinHttpClient.ReadFromJsonAsync` calls are on responses already validated by the `ErrorTranslationHandler` pipeline layer, which has already consumed the body for error detection. PASS.
