---
id: TASK-014
status: IMPLEMENTED
---

# TASK-014: OkxHttpClient + interface

**Milestone**: M-OKX
**Wave**: 9
**Group**: 9
**Status**: PLANNED
**Depends on**: TASK-013
**Retry count**: 0/3
**Delegates to**: none
**Traces to**: research#okx (REST transport over shared resilience pipeline)
**Blast radius**: LOW — new files in Okx project.

## Description
Implement `IOkxHttpClient` and `OkxHttpClient` (`GetAsync<T>`, `PostAsync<T>`, `DeleteAsync<T>`) mirroring the Binance/Bybit wrapper. GET uses an escaped query string appended to the request path; POST sends a JSON body. Marks signed requests via `OkxSigningRequest.MarkSigned`. The request path passed downstream must match what the signer hashes (path + query for GET) — ensure path/query construction is consistent with `OkxSigningHandler`'s prehash assembly. Case-insensitive `System.Text.Json` deserialization.

## File Scope
### Creates
- `src/CryptoExchanges.Net.Okx/IOkxHttpClient.cs`
- `src/CryptoExchanges.Net.Okx/OkxHttpClient.cs`
### Modifies
- (none)

## Files modified
- src/CryptoExchanges.Net.Okx/IOkxHttpClient.cs
- src/CryptoExchanges.Net.Okx/OkxHttpClient.cs

## Pattern Reference
- `src/CryptoExchanges.Net.Binance/BinanceHttpClient.cs:26-68` (Get/Post/Delete, EscapeDataString, JSON options)
- `src/CryptoExchanges.Net.Bybit/BybitHttpClient.cs` (JSON-body POST + signed marker reference from TASK-005)

## Acceptance Criteria
1. `GetAsync<T>` builds an escaped path+query and marks signed when requested; `PostAsync<T>` sends a JSON body and marks signed.
2. The request path+query handed to the pipeline is byte-consistent with the prehash the `OkxSigningHandler` computes (no signature mismatch).
3. `IOkxHttpClient` is internal and exposed to the integration test project via `InternalsVisibleTo`.

## Test Requirements
- Integration tests in TASK-015 assert URL/body/signed-marker via stub handler; service unit tests mock `IOkxHttpClient`.

## Implementation Notes
Created two files, mirroring the Bybit V5 wrapper (the most recent JSON-body exchange wrapper):
- `src/CryptoExchanges.Net.Okx/IOkxHttpClient.cs` — `internal interface IOkxHttpClient` with `GetAsync<T>`/`PostAsync<T>`/`DeleteAsync<T>`, each `(string endpoint, Dictionary<string,string>? parameters = null, bool signed, CancellationToken ct = default)`. GET defaults `signed: false`; POST/DELETE default `signed: true` (same defaults as Bybit). XML docs on every member.
- `src/CryptoExchanges.Net.Okx/OkxHttpClient.cs` — `internal sealed class OkxHttpClient(HttpClient httpClient) : IOkxHttpClient`. Ctor takes ONLY `HttpClient` (recv-window/passphrase/signing are not the client's concern, per ADR-001).

**Query building (GET/DELETE):** `BuildUrl` appends an escaped query string to the endpoint path — `Uri.EscapeDataString` on each key and value joined with `&`, prefixed with `?` only when parameters exist (e.g. `/api/v5/market/tickers?instType=SPOT`). Identical helper to Bybit's `BuildUrl`/`BuildQueryString`.

**JSON-body POST:** `JsonSerializer.Serialize(parameters ?? [], JsonOptions)` is wrapped in `StringContent(json, Encoding.UTF8, "application/json")`. The serialized JSON is the verbatim wire body — the signing handler reads it back via `request.Content.ReadAsStringAsync` to compute the signature, so the body string must not be re-serialized or mutated downstream.

**RequestUri.PathAndQuery sign-consistency (CRITICAL):** `OkxSigningHandler.ResignAsync` signs `request.RequestUri!.PathAndQuery` (prehash = `timestamp + METHOD + requestPath + body`). For the path the handler hashes to equal what the wire receives, the configured `HttpClient.BaseAddress` is the host root only (`https://www.okx.com`, NO path) and THIS client builds the full relative request path beginning with `/api/v5/...` plus the escaped query. When that relative URI is resolved against the host-only base address, `RequestUri.PathAndQuery` is exactly the OKX `requestPath` to be signed — byte-for-byte. Services therefore pass full paths like `/api/v5/market/tickers` as the endpoint. `ArgumentException.ThrowIfNullOrWhiteSpace(endpoint)` guards every method.

**Signed marker:** when `signed: true`, the client calls `OkxSigningRequest.MarkSigned(request)` and does NOT sign inline — signing/timestamp/header assembly is the handler's job (re-signed per attempt below the retry strategy). The client only sets the marker, builds the URL, and sets the body.

**JSON options:** `PropertyNameCaseInsensitive = true`, `NumberHandling = JsonNumberHandling.AllowReadingFromString`, `DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull` — mirrors Bybit's `JsonOptions` block exactly.

**Internals visibility:** `IOkxHttpClient`/`OkxHttpClient` are `internal`; the Okx csproj already grants `InternalsVisibleTo` to `CryptoExchanges.Net.Okx.Tests.Integration` (URL/body/marker assertions) and `DynamicProxyGenAssembly2` (NSubstitute mocking of `IOkxHttpClient` in unit tests). No csproj edit needed.

**Deviations from the Bybit wrapper:** none of substance. Signatures, defaults, JSON options, query/body construction, guards, and `ConfigureAwait(false)` are identical; only names (`Okx*`/`OkxSigningRequest`) and the XML-doc commentary (OKX header names and the host-only-BaseAddress sign-consistency rationale) differ. No Http/Core edits.

## Verification
- `dotnet build CryptoExchanges.Net.sln` → Build succeeded, 0 Warning(s), 0 Error(s).
- No new tests this task (TASK-015 covers them).

## Commits
- (recorded below)
