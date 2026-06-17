---
id: TASK-005
status: IMPLEMENTED
---

# TASK-005: BybitHttpClient + interface

**Milestone**: M-BYBIT
**Wave**: 3
**Group**: 3
**Status**: PLANNED
**Depends on**: TASK-004
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#bybit (REST transport over shared resilience pipeline)
**Blast radius**: LOW — new files in Bybit project.

## Description
Implement `IBybitHttpClient` and `BybitHttpClient` (internal HTTP wrapper: `GetAsync<T>`, `PostAsync<T>`, `DeleteAsync<T>`) mirroring the Binance wrapper. Build query strings with `Uri.EscapeDataString`, mark signed requests via `BybitSigningRequest.MarkSigned`, deserialize with `System.Text.Json` (`PropertyNameCaseInsensitive = true`). POST uses JSON body (Bybit V5 is JSON-bodied), not form-encoding — this is the key transport delta from Binance.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Bybit/IBybitHttpClient.cs`
- `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs`
### Modifies
- (none)

## Files modified
- src/CryptoExchanges.Net.Bybit/IBybitHttpClient.cs
- src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Binance/IBinanceHttpClient.cs` (mockable internal interface)
- `src/CryptoExchanges.Net.Binance/BinanceHttpClient.cs:26-68` (Get/Post/Delete, EscapeDataString, JSON options)

## Acceptance Criteria
1. `GetAsync<T>` builds an escaped query string and marks the request signed when `signed: true`; `PostAsync<T>` sends a JSON body and marks signed.
2. JSON deserialization is case-insensitive and returns the typed Bybit DTO; signing itself is left to `BybitSigningHandler` (no inline signing in the client).
3. `IBybitHttpClient` is `internal` and visible to the integration test project via `InternalsVisibleTo`.

## Test Requirements
- Integration tests in TASK-008 use a stub handler to assert URL/body/signed-marker shape; unit tests mock `IBybitHttpClient` for services.

## Implementation Notes

### Files created
- `src/CryptoExchanges.Net.Bybit/IBybitHttpClient.cs` — internal interface: `GetAsync<T>`, `PostAsync<T>`, `DeleteAsync<T>`. Mirrors the Binance wrapper signatures (`endpoint`, optional `Dictionary<string,string>? parameters`, `bool signed`, `CancellationToken`). Visible to the integration test project and the DI package via the existing `InternalsVisibleTo` entries in the csproj.
- `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs` — internal sealed wrapper. Builds escaped query strings with `Uri.EscapeDataString`, marks signed requests via `BybitSigningRequest.MarkSigned`, and deserializes with `System.Text.Json` (`PropertyNameCaseInsensitive = true`, plus `AllowReadingFromString` and `WhenWritingNull` matching the Binance options block).

### Deviations from the Binance pattern (with justification)
1. **POST sends a JSON body, not form-encoding.** Bybit V5 is JSON-bodied. `PostAsync` serializes `parameters` with `JsonSerializer.Serialize` into a `StringContent` with media type `application/json` (Binance uses `application/x-www-form-urlencoded`). The serialized JSON is the verbatim wire body — important because `BybitSigningHandler` reads the request content back as a string and signs that exact text (`BuildPostSignString`). GET/DELETE still carry params in the escaped query string, which the handler reads from `RequestUri.Query` for `BuildGetSignString`.
2. **No `recvWindow` injected into the query or body.** Binance's `BuildBaseQuery` appends `recvWindow=...` to the signed payload. Bybit carries recv-window in the `X-BAPI-RECV-WINDOW` header, which `BybitSigningHandler` adds itself. So the client builds a plain query / plain JSON body and never touches recv-window. This is why `BuildQueryString` is the only query helper needed (no `BuildBaseQuery` equivalent).
3. **No `BybitOptions` constructor parameter.** Binance injects `BinanceOptions` solely to read `ReceiveWindow`. Because recv-window is owned by the signing handler here, the client has no use for options; injecting it would trip CS9113 (unread primary-constructor parameter) under TreatWarningsAsErrors. The constructor takes only `HttpClient`.
4. **No `GetStringAsync`.** The Binance interface exposes a raw-string GET; it is not in TASK-005's required surface (`GetAsync`/`PostAsync`/`DeleteAsync`), so it was omitted. Can be added later if a Bybit service needs raw-string reads.
5. **Signing is left entirely to `BybitSigningHandler`.** The client only sets the signed marker and builds the URL/body; no inline timestamp/HMAC.

### Verification
- `dotnet build CryptoExchanges.Net.sln` → `Build succeeded. 0 Warning(s) 0 Error(s)`.
- No tests added (per scope; tests arrive in TASK-008). Did not commit.

## Commits
- **Commit**: 2a598c8 feat(M2): TASK-005 BybitHttpClient + IBybitHttpClient (JSON-body POST)
