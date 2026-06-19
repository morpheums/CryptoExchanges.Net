# Security Review — TASK-031 (Cycle 2)

## Verdict: APPROVED

## Score: 98/100

## Prior Non-Blocking Concerns — Status

### FormatException category mismatch for asset errors: RESOLVED
The remediation replaced the `FormatException` throw in `GetBalance` with a direct `Task.FromResult(ToolResult<AssetBalance>.Failure(new ToolError("BadRequest", ...)))` at `AccountTools.cs:41-43`. The bad-asset path no longer reaches `ToolRunner` at all. The error category is now explicitly `"BadRequest"`, which is semantically correct and precisely tested by `GetBalance_BadAsset_ReturnsBadRequest` asserting `result.Error!.Category.Should().Be("BadRequest")`. The prior mismatch is fully eliminated.

### `limit` parameter not validated: STILL PRESENT (non-blocking, unchanged)
`GetOrderHistory` and `GetTradeHistory` still pass `limit` unbounded to the exchange service layer. The remediation did not address this, and it was not a blocking concern. No regression introduced — the exchange service layer continues to be responsible for range enforcement.

## Core Security Properties

### Read-only scope still enforced: PASS
All six tool methods call exclusively read methods. The diff introduces no new tool methods and no references to `PlaceOrder`, `CancelOrder`, `Withdraw`, `Transfer`, or any mutating interface method. Full grep of `AccountTools.cs` for write-method names returns empty.

### AuthenticationException still maps to AuthRequired: PASS
`ToolRunner.Categorize` is unchanged. The `AuthenticationException => "AuthRequired"` arm at `ToolRunner.cs:32` precedes the `ExchangeApiException` base-class arm. Tests `GetBalances_MissingCredentials_MapsToAuthRequired` and `GetOpenOrders_MissingCredentials_MapsToAuthRequired` continue to assert this mapping. No regression.

### No credential exposure in error messages: PASS
The new `ToolError("BadRequest", $"Unknown or empty asset '{asset}'.")` message at `AccountTools.cs:43` reflects only the caller-supplied asset string, which is the user's own input. It does not incorporate `ApiKey`, `SecretKey`, or any exchange-internal secret. `ToolRunner` is bypassed for this path, so `ex.Message` forwarding is not relevant here. All other paths continue to use `ToolRunner.RunAsync` which surfaces `ex.Message` with the same guarantees as cycle 1.

### No key leak through exception messages: PASS
No changes to exception construction paths in the diff. `AuthenticationException` messages remain exchange-supplied signature-validation text, not reflections of locally held key material.

### IExchangeClientFactory usage still correct: PASS
The factory lookup order in `GetBalance` was adjusted: `Asset.TryOf` is now checked before `TryParseExchange` / `factory.TryGet`. This is a valid short-circuit optimization — the factory is accessed only after the local domain validation passes. `TryGet` remains the only factory access method used throughout `AccountTools`. No direct credential store access introduced.

### No static credential state: PASS
`AccountTools` remains a static class with only two `private const string` description fields. The new `GetBalance_ReturnsData`, `GetOrder_ReturnsData`, and `GetOrderHistory_ReturnsData` tests operate on per-test substitutes. No static mutable state introduced.

### Direct ToolResult.Failure return for bad-asset does not bypass security-relevant ToolRunner processing: PASS
`ToolRunner.RunAsync` is an exception-to-ToolError translator, not a security gate. It performs no authentication check, no credential validation, no rate-limit enforcement, and no signing. The only processing it provides is: (a) wrapping the result in a success envelope, and (b) catching exceptions and categorizing them. For the bad-asset early-exit path, there is nothing to sign or authenticate — no exchange call is made. Bypassing `ToolRunner` here is correct by design and introduces zero security concern. The direct `ToolResult.Failure` return is structurally identical to the already-established `Unavailable(exchange)` early-exit pattern used throughout the same method.

## New Findings

None. The remediation is narrowly scoped to the bad-asset path refactor and three happy-path test additions. No new security surface was introduced. The dead mock removal and `client!` alignment are cosmetic. Full grep for `SecretKey`, `ApiKey`, `ToString`, `JsonSerializer`, `log`, and `RawBody` in `AccountTools.cs` returns empty.

## Summary

The remediation resolves the prior cycle 1 concern cleanly: the bad-asset path in `GetBalance` now returns a correctly categorized `"BadRequest"` `ToolError` directly at the MCP boundary, which is architecturally sound and fully tested. All seven core security properties hold without regression. No new findings. The score increases from 96 to 98 reflecting the resolved concern; the remaining 2-point gap is the still-present `limit` non-validation noted in both cycles.
