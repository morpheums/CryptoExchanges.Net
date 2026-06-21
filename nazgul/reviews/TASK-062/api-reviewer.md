---
reviewer: api-reviewer
verdict: APPROVE
---
# TASK-062 API Review (Cycle 2)

## Verdict: APPROVE

---

## Cycle-1 Blocking Finding Re-check

### APPROVE — KucoinStreamOptions.RestBaseUrl is now consumed and drives bullet-public host (confidence: 99%)

**Cycle-1 finding**: `RestBaseUrl` was a public option that the `protocolFactory` never read — setting it was a silent no-op.

**Fix applied** (`StreamServiceCollectionExtensions.cs` lines 37–49):

1. `KucoinStreamOptions` is resolved from the service provider (`sp.GetRequiredService<KucoinStreamOptions>()`).
2. LR-001 guard applied: `ArgumentException.ThrowIfNullOrWhiteSpace(options.RestBaseUrl)` fires before consumption.
3. Absolute-URI validation: `Uri.TryCreate(..., UriKind.Absolute, out var baseUri)` — throws `ArgumentException` with a human-readable message including the bad value on failure.
4. `httpClient.BaseAddress = baseUri` is set on the fresh `HttpClient` instance returned by `IHttpClientFactory.CreateClient(KucoinClientName)`.
5. `KucoinBulletPublicClient` receives that configured client. `KucoinHttpClient.PostJsonAsync` passes the path (`/api/v1/bullet-public`) as a relative `Uri` string to `HttpRequestMessage`, which .NET combines with `BaseAddress` — the override is therefore effective.

**Isolation check** (Verify point 2): `IHttpClientFactory.CreateClient()` returns a new `HttpClient` instance on each call. The `BaseAddress` mutation on line 49 affects only the locally-scoped instance created inside the `protocolFactory` lambda. The shared REST client (registered separately via `AddKucoinExchange` / `ExchangeServiceRegistration.AddExchange`) is a different factory-managed instance and is unaffected. Isolation is correct.

**Validation guard check** (LR-001 / Verify point 3): Both failure modes are covered — null/whitespace caught by `ThrowIfNullOrWhiteSpace`, non-absolute URI caught by `Uri.TryCreate` with `UriKind.Absolute`. Error messages are clear and include the offending value.

**Blocking finding is fully resolved.**

---

## New Test Coverage

### APPROVE — KucoinStreamOptionsWiringTests proves wiring without network (confidence: 99%)

`KucoinStreamOptionsWiringTests` (4 tests) satisfies Verify point 4:

- `AddKucoinStreams_CustomRestBaseUrl_IsHostUsedForBulletPublicNegotiation` — injects a `CapturingHandler` as the primary handler of the named `kucoin` client, sets `RestBaseUrl = "https://sandbox-api.kucoin.com"`, calls `NegotiateAsync`, and asserts `LastRequestUri.Host == "sandbox-api.kucoin.com"` and path `== "/api/v1/bullet-public"`. This is a direct proof of the fix.
- `AddKucoinStreams_DefaultRestBaseUrl_NegotiatesAgainstProductionHost` — verifies the default (`api.kucoin.com`) is preserved.
- `AddKucoinStreams_WhitespaceRestBaseUrl_FailsFast` — confirms `ArgumentException` on whitespace input.
- `AddKucoinStreams_RelativeRestBaseUrl_FailsFast` — confirms `ArgumentException` on a non-absolute URI.

The `CapturingHandler` is a no-network `HttpMessageHandler` that returns a canned valid bullet-public JSON payload, so the negotiation path runs fully through production code. The `ExtractBulletClient` helper uses reflection to reach the internal `IKucoinBulletPublicClient`, which is the appropriate technique for white-box testing of an internal type. No new API-surface exposure is introduced by the test.

---

## New Defect Check (Verify point 5)

### APPROVE — No new API-surface defects or contract breaks introduced (confidence: 99%)

- `KucoinStreamOptions` public surface is unchanged: still one property `RestBaseUrl` with the same type, same default, same XML doc.
- `AddKucoinStreams` signature is unchanged: `Action<KucoinStreamOptions>? configure = null`.
- No new public types, no interface modifications, no enum changes.
- `StreamServiceCollectionExtensions.KucoinClientName` was already `internal const` in cycle 1; no visibility change.
- LR-004 (array-indexing guard on `InstanceServers`) was PASS in cycle 1 and is unaffected by this change.
- The `PostJsonAsync` path (`KucoinHttpClient.cs:68`) constructs `HttpRequestMessage` with the relative path string and relies on `BaseAddress` for host resolution — this is the standard .NET `HttpClient` contract and the fix exploits it correctly.

---

## Previously Passing Findings (Unchanged)

All cycle-1 PASS findings remain PASS; none of the files they cover were modified.

- PASS: LR-004 empty-list guard on `InstanceServers`.
- PASS: `IStreamProtocol` contract compliance.
- PASS: `AddKucoinStreams` DI parity with `AddBinanceStreams`.
- PASS: `ExchangeId.Kucoin` consistency.
- PASS: `StreamKind` coverage in `BuildTopic` and decoder registry.
- PASS: `IKucoinBulletPublicClient` internal interface and testability.

Previously non-blocking concerns are unchanged and do not block approval:

- CONCERN: Duplicate `KucoinClientName = "kucoin"` constant in `ServiceCollectionExtensions` (private) and `StreamServiceCollectionExtensions` (internal) — confidence 90%, non-blocking.
- CONCERN: `ValidateOnStart` is a no-op without `DataAnnotations` attributes on `KucoinStreamOptions` — confidence 85%, non-blocking, consistent with Binance pattern.

---

## Summary

- APPROVE (was REJECT): `KucoinStreamOptions.RestBaseUrl` is now resolved, guarded (LR-001), validated as absolute URI, and applied to the bullet-public `HttpClient` instance — the option correctly drives the negotiation host. Unit tests confirm the wiring without touching the network.
- APPROVE: No new API-surface defect, contract break, or LR-004 regression introduced by the fix.
