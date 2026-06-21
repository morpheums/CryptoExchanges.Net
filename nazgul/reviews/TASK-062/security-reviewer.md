---
reviewer: security-reviewer
verdict: APPROVE
---
# TASK-062 Security Review

## Verdict: APPROVE

## Findings

### [APPROVE] SSRF via server-returned WS endpoint — host/scheme validation present (confidence: 98%)

`KucoinStreamProtocol.ValidateWsEndpoint` (lines 180-197) is called before the URI is constructed. It enforces two constraints:
1. Scheme must be exactly `wss` (case-insensitive).
2. Host must equal `kucoin.com` or end with `.kucoin.com` (case-insensitive).

Tests `ResolveConnectionAsync_NonKucoinHost_Throws` and `ResolveConnectionAsync_HttpScheme_Throws` cover both rejection paths. The validation is proportionate to the threat model: this is a public client library where the user controls `RestBaseUrl` (the HTTP origin for the bullet-public call), so a MITM or DNS-spoofed bullet-public response is the only realistic SSRF vector. The hardcoded host-suffix whitelist is the correct defense here. No finding.

### [APPROVE] Token in WebSocket URI — no logging, no exception messages (confidence: 97%)

The bullet-public token is placed only in the `Uri` instance returned inside `StreamConnectionInfo`. No code path logs it, includes it in exception messages, or serializes it. Exception messages in `ValidateWsEndpoint` include only host and scheme strings, never the token. `BulletPublicDto` is `internal sealed record` with no `[JsonInclude]`, no `ToString()` override, and no serialization attribute — the compiler-generated `ToString()` would expose field values, but since the type is fully internal and never handed to a logger or serializer boundary in this diff, this is acceptable. No finding.

### [APPROVE] URI construction safety — Uri constructor with EscapeDataString (confidence: 98%)

Line 45 of `KucoinStreamProtocol.cs`:
```
var uri = new Uri($"{server.Endpoint.TrimEnd('/')}?token={Uri.EscapeDataString(bullet.Token)}&connectId={connectId}");
```
- `server.Endpoint` was already parsed and validated by `Uri.TryCreate` inside `ValidateWsEndpoint`, so it is a well-formed absolute URI string. `TrimEnd('/')` is safe on a validated URI string.
- `bullet.Token` is passed through `Uri.EscapeDataString`, preventing injection via a crafted token value.
- `connectId` is `Guid.NewGuid().ToString("N")` — 32 hex characters, no special chars that need escaping. The final `new Uri(...)` constructor validates the fully-formed string, throwing `UriFormatException` if any path is malformed. No finding.

### [APPROVE] connectId uniqueness — Guid.NewGuid() (confidence: 99%)

Line 44: `var connectId = Guid.NewGuid().ToString("N")`. Each call to `ResolveConnectionAsync` produces a fresh cryptographically random GUID. The test `ResolveConnectionAsync_EachCall_HasUniqueConnectId` asserts that two sequential calls produce distinct query strings. No finding.

### [APPROVE] No API keys / signing for bullet-public (confidence: 99%)

`KucoinBulletPublicClient.NegotiateAsync` passes `signed: false` to `_http.PostAsync`. The DI comment in `StreamServiceCollectionExtensions` (line 36) explicitly notes "Bullet-public is unauthenticated (signed: false), so the signing handler is a no-op." The named `kucoin` HttpClient pipeline's signing handler is a no-op when `signed: false`, consistent with the established pattern in `BinanceSigningHandler`. No API key is transmitted to the bullet-public endpoint. No finding.

### [CONCERN] Decoder closures propagate JsonException and NullReferenceException to caller — no per-frame try/catch (confidence: 75%)

The four decoder closures in `KucoinStreamDecoders` (lines 43-119) do not wrap their `JsonSerializer.Deserialize<T>(...)!` calls in try/catch. Two failure modes exist:

1. **JsonException** from a malformed data payload: `ExtractDataBytes` catches `JsonException` on the outer frame parse and falls through, returning the original bytes. However, the subsequent `JsonSerializer.Deserialize<T>` inside the closure itself is not guarded. A malformed `data` sub-object (valid outer frame, invalid inner JSON that `ExtractDataBytes` successfully extracted as bytes but that fails schema deserialization) will throw `JsonException` to whatever calls the closure — presumably the engine's dispatch loop.

2. **NullReferenceException** from `!` (null-forgiving): `JsonSerializer.Deserialize<T>` returns `null` when the JSON token is a JSON `null` literal. Using `!` suppresses the compiler warning but does not prevent a `NullReferenceException` at runtime if an exchange returns `{"data":null}`.

3. **IndexOutOfRangeException** in the OrderBook decoder: `b[0]`, `b[1]`, `a[0]`, `a[1]` are accessed without bounds checking on the inner `List<string>` entries. KuCoin documents the format as `[price, size, sequence]` but a malformed frame with an entry having fewer than 2 elements will throw `IndexOutOfRangeException`.

Severity assessment: whether this crashes the connection or is gracefully swallowed depends entirely on how the engine's dispatch loop handles exceptions from decoder closures. If the engine wraps decoder invocations in try/catch and emits a frame-level error without disconnecting, these are benign. If the engine lets them propagate, a single malformed frame from a compromised or buggy exchange server could crash the stream connection. Since the engine code is not in scope for this diff, confidence is 75% (non-blocking per the review rules). The established pattern in `BinanceErrorTranslator.cs:36-50` shows defensive handling for malformed JSON; the same defensive posture in decoders would be consistent.

**Suggestion**: Wrap each decoder closure body in try/catch (catching at minimum `JsonException`, `ArgumentOutOfRangeException`, and `IndexOutOfRangeException`), returning a sentinel or rethrowing a typed `StreamFrameDecodeException`. Alternatively, validate entry count before indexing: `b.Count >= 2 ? KucoinValueParsers.ParseDecimal(b[0]) : 0m`.

### [APPROVE] No secrets in test fixtures (confidence: 99%)

Test fixtures in `KucoinStreamProtocolTests` use clearly placeholder values: `token = "test-token"`, `token = $"token-{CallCount}"`. No real API keys, HMAC secrets, or PII appear anywhere in the diff. No finding.

### [APPROVE] KucoinStreamOptions — no secrets, no serialization risk (confidence: 99%)

`KucoinStreamOptions` carries only `RestBaseUrl` (a non-secret configuration value). No `SecretKey`, `ApiKey`, or credential field is present. No `[JsonInclude]` attributes. No serialization path. No finding.

### [APPROVE] BulletPublicDto / InstanceServerDto — no credential fields, internal only (confidence: 99%)

Both DTOs are `internal sealed record`. No `[JsonInclude]`. No credential fields. The `Token` field is a short-lived bearer token (not an API secret), consistent with embedding it in a URI query parameter per KuCoin's documented protocol. No finding.

---

## Summary

- PASS: SSRF endpoint validation — `wss://` scheme + `*.kucoin.com` host whitelist enforced before URI construction; two rejection tests present.
- PASS: Token exposure — token appears only in the returned `Uri`, never in exception messages or serialization paths.
- PASS: URI construction — `Uri.EscapeDataString` used on token; endpoint pre-validated by `Uri.TryCreate`; final `new Uri(...)` validates the composed string.
- PASS: connectId uniqueness — `Guid.NewGuid()` per connection; tested.
- PASS: No signing for bullet-public — `signed: false` confirmed; DI comment documents intent.
- PASS: Test fixtures — placeholder tokens only, no real credentials.
- PASS: KucoinStreamOptions — no credential fields, no serialization risk.
- PASS: DTOs — internal, no `[JsonInclude]`, no credential fields.
- CONCERN: Decoder closures — no per-frame try/catch around `JsonSerializer.Deserialize` or index access; malformed frames could propagate `JsonException`, `NullReferenceException` (from `!` null-forgiving), or `IndexOutOfRangeException` (OrderBook `b[0]`/`b[1]` without count guard) to the engine dispatch loop. Severity depends on engine resilience; confidence 75%, non-blocking.
