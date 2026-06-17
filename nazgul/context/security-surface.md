# Security Surface

## Authentication
- HMAC-SHA256 API key signing: `src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs`
- `BinanceSigningHandler` injects timestamp + HMAC signature into each signed request: `src/CryptoExchanges.Net.Binance/Resilience/BinanceSigningHandler.cs`
- API key added as `X-MBX-APIKEY` request header: `ServiceCollectionExtensions.cs:75-76`
- Secret key never leaves the `BinanceSignatureService` — only the HMAC output is appended to the query: architecture ensures `SecretKey` is used only in signing, not transmitted
- Key-only mode supported (no signing when `SecretKey` is empty): `BinanceClientComposer.cs:70-76`

## Authorization
- Exchange-level: Binance's own RBAC (API key permissions). The SDK passes credentials to the exchange and surfaces `AuthenticationException` for Binance codes -1022, -2014, -2015, -1021 and HTTP 401/403: `BinanceErrorTranslator.cs:22-24`
- No SDK-level authorization layer (by design — library delegates to exchange)

## Input Validation
- `ArgumentNullException.ThrowIfNull(...)` used consistently as guard pattern: `SymbolMapper.cs:27`, `ErrorTranslationHandler.cs:24`, `ExchangeResiliencePipeline.cs:23-24`, `ServiceCollectionExtensions.cs:38`
- `ArgumentException.ThrowIfNullOrWhiteSpace(...)`: `SymbolMapper.cs:76`
- Domain-level: `Symbol` constructor rejects `Asset.None` and same-asset pairs: `Models.cs:20-23`
- `Asset.TryOf()` / `Asset.Of()` validate ticker format (A-Z0-9, max 32 chars): `Asset.cs:67-94`
- `PlaceOrderRequest.Validate()` enforces required fields per order type: `IExchangeClient.cs:257-290`
- `BinanceOptions` validated at startup via `ValidateOnStart`: `ServiceCollectionExtensions.cs:46-48`
- No third-party validation library (Fluent Validation, etc.) — hand-written guards

## Data Sanitization
- No HTML output, no SQL queries — not applicable (HTTP client library)
- Query string parameters are URI-escaped: `BinanceHttpClient.cs:91` (`Uri.EscapeDataString`)
- JSON deserialization uses `System.Text.Json` with `PropertyNameCaseInsensitive = true`: `BinanceHttpClient.cs:19-23`

## Secrets Management
- API key and secret via `BinanceOptions` properties or env vars `BINANCE_API_KEY` / `BINANCE_SECRET_KEY`: `BinanceExchangeClient.cs:93-94`
- No `.env` files, vault, or secret manager integration
- `SecretKey` is held in-memory only within `BinanceOptions` and `BinanceSignatureService`
- Risk: if `BinanceOptions` is logged, secrets could leak — no explicit log-scrubbing/masking

## CORS/CSP
- Not applicable — this is a .NET library, not a web application

## Rate Limiting
- Client-side rate-limit enforcement via `RateLimitThrottleHandler` + `ReactiveRateLimitGate`: `RateLimitThrottleHandler.cs`
- The gate observes `Retry-After` response headers and Binance weight headers (`X-MBX-USED-WEIGHT-1m`): `ServiceCollectionExtensions.cs:90-91`
- Server-side 429 / HTTP 418 responses produce `RateLimitExceededException` with `RetryAfter` field: `BinanceErrorTranslator.cs:19-20`, `TransientExhaustionHandler.cs:91-98`

## Known Vulnerable Patterns
- None detected — no SQL concatenation, no `eval()`, no `innerHTML`, no `Process.Start` with user input
- Retry on mutation risk: `ExchangeResiliencePipeline.cs:38` correctly gates retries to GET-only requests
- `BinanceSigningRequest.MarkSigned()` prevents double-signing on retry: `BinanceSigningHandler.cs` (confirmed by `BinancePipelineEndToEndTests.cs:46-50`)

## Recommendations
- Add XML documentation warning for `BinanceOptions.SecretKey` advising callers not to log this object
- Consider adding a custom `ToString()` on `BinanceOptions` that redacts `ApiKey` and `SecretKey` to prevent accidental logging
- Add CI pipeline: currently no automated secret-scan or SAST in CI (no CI detected at all)
- Consider adding a CI step that fails the build if secrets-like patterns appear in committed code
