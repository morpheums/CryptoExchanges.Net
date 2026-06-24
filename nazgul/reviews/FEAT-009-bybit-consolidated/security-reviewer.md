# Security Review — FEAT-009 Bybit Consolidated

## Verdict: APPROVED

## Blocking Findings

None.

## Non-Blocking Concerns

`src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamProtocol.cs:165-173` — `BuildBatch` interpolates `WireSymbol` directly into a raw JSON string via `StringBuilder.Append`. `WireSymbol` is produced by `SymbolMapper.ToWire`, which upper-cases ticker components from `Asset.Ticker`; tickers are alphanumeric-only in practice. However, there is no explicit validation or JSON-encoding of the topic string before embedding it into the JSON frame. A caller who constructs `Asset.Of("BTCUSDT\"}]}evil")` would produce a malformed/injected subscribe frame. The risk is low because `Asset.Of` performs its own validation upstream and exchange symbols are strictly alphanumeric, but the frame builder does not guarantee safety by construction — Confidence: 45/100 (non-blocking).

`src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamDecoders.cs:122-141` — `InvalidOperationException` messages in `DeserializeData` and `DeserializeFirstArrayElement` include `typeof(T).Name` (i.e., DTO type names such as `StreamTradeDto`). These are internal implementation type names, not market data values, so there is no PII or credential leakage. However, if callers ever surface these exceptions through a public API error path, internal type names become visible. Confidence: 20/100 (non-blocking, very low risk).

`src/CryptoExchanges.Net.Bybit/Streaming/BybitStreamOptions.cs:12` — `StreamBaseUrl` is a plain `string` with no validation that the resolved URI uses the `wss://` scheme. The `Uri` constructor in `BybitStreamProtocol` (line 58) accepts any scheme; a mis-configured `ws://` URL would silently downgrade to an unencrypted connection. There is no `if (endpoint.Scheme != "wss") throw` guard. Confidence: 60/100 (non-blocking — the default is correct and override is an operator concern, but a defensive check would be consistent with the codebase's fail-fast style).

## Checklist Results

1. WSS endpoint — PASS. `BybitStreamOptions.StreamBaseUrl` defaults to `wss://stream.bybit.com/v5/public/spot` (`BybitStreamOptions.cs:12`). `BybitStreamProtocol` constructor calls `ArgumentNullException.ThrowIfNull(options)` at line 57 before using `options.StreamBaseUrl`. The `Uri` constructor at line 58 accepts any caller-supplied scheme without TLS enforcement; this is a low-confidence concern (see Non-Blocking Concerns above), not a blocking defect.

2. No credential handling in public streams — PASS. `BybitStreamProtocol.BuildSubscribe`, `BuildUnsubscribe`, and `BuildBatch` produce frames with only `req_id`, `op`, and `args` fields (`BybitStreamProtocol.cs:86-95, 165`). No API key, secret, or signature field is present. All four streaming DTOs (`StreamTickerDto`, `StreamTradeDto`, `StreamDepthDto`, `StreamKlineDto`) contain only market-data fields; none carry credential-like properties.

3. Input validation on options — PASS. `StreamBaseUrl` is consumed only by the `Uri` constructor (`BybitStreamProtocol.cs:58`) to build the WebSocket endpoint. It is never passed to a format string, shell invocation, or command builder. No injection risk beyond the URL scheme concern noted above.

4. No unbounded buffers — PASS. `StreamDepthDto.Bids` and `StreamDepthDto.Asks` are `List<List<string>>` (`StreamDepthDto.cs:19,24`). Their size is bounded server-side by Bybit's depth parameter (1, 50, or 200 levels per the confirmed Bybit v5 spec). The decoder pre-sizes `bids` and `asks` lists from `dto.Bids.Count` and `dto.Asks.Count` (`BybitStreamDecoders.cs:77,82`), so no secondary unbounded allocation occurs. No new Bybit-specific types introduce unbounded collections.

5. JSON parsing exception safety — PASS. `BybitStreamProtocol.Classify` wraps all `JsonDocument` operations in a `try/catch (JsonException)` block (`BybitStreamProtocol.cs:147-150`) and returns `new StreamFrame(FrameKind.Error, null)` on any parse failure. Malformed frames from the wire do not propagate as uncaught exceptions.

6. No PII in logs/exceptions — PASS. These are public market-data streams; no user-identifying information is present. Exception messages in decoders reference DTO type names and `JsonValueKind` enum values only (`BybitStreamDecoders.cs:136,140`). Symbol strings from the wire appear in `FormatException` messages inside `SymbolMapper.FromWire` only (pre-existing code, out of scope). No new logging calls were introduced.

7. Integration test credentials — PASS. `BybitStreamingSmokeTests.BuildStreamClient()` explicitly sets `o.ApiKey = string.Empty` and `o.SecretKey = string.Empty` (`BybitStreamingSmokeTests.cs:69-70`). No real credentials appear anywhere in the test file.

8. Attack surface of `AddBybitStreams()` — PASS. `StreamServiceCollectionExtensions.AddBybitStreams` delegates entirely to `StreamServiceRegistration.AddStreams<BybitStreamOptions>`, which registers only keyed singletons (`IStreamClient` keyed by `ExchangeId.Bybit`) and per-exchange options. The shared `IStreamClientFactory` is registered with `TryAddSingleton` (first-registration-wins). No open-ended or unkeyed service registrations are introduced that could be exploited by a container-level attacker.

## Summary

All eight checklist items pass. The implementation is correctly scoped to public, unauthenticated market-data streams with no credential handling anywhere in the new code. The WSS default is correct; JSON parsing exceptions are properly caught and converted to `FrameKind.Error`; integration test credentials are empty strings. Three low-to-medium-confidence non-blocking concerns are noted: the absence of a `wss://` scheme guard on custom URL overrides, JSON-injection-by-construction in the batch subscribe builder (mitigated by upstream `Asset` validation), and internal DTO type names appearing in `InvalidOperationException` messages. None of these are blocking; all are consistent with acceptable risk for a public SDK targeting informed operators.
