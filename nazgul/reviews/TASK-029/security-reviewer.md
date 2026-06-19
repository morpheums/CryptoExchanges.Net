# Security Review — TASK-029

## Verdict: APPROVED
Confidence: 95

## Summary
The three new files (ToolResult.cs, ToolInputs.cs, ToolRunner.cs) introduce a thin MCP boundary layer that translates typed SDK exceptions to structured error envelopes. No credentials are referenced, stored, or logged anywhere in the new code. The exception message surfaced via ex.Message in ToolError.Message comes exclusively from the SDK typed exception constructors, which are constructed from exchange-supplied text (not from ApiKey or SecretKey). Input parsing via ParseSymbol / Asset.TryOf is safe against injection and resource exhaustion. The broad catch boundary at the MCP layer is intentional and correctly documented. One low-severity informational note is raised about RawBody not being forwarded -- this is actually the correct behavior.

## Findings

### ex.Message in ToolError -- No credential leakage confirmed
- Severity: INFO
- Confidence: 97
- Blocking: no
- File: src/CryptoExchanges.Net.Mcp/ToolRunner.cs:24
- Detail: ToolError.Message is set to ex.Message. All exchange exceptions (AuthenticationException, RateLimitExceededException, etc.) construct their messages in BinanceErrorTranslator.cs:17 as "Binance error {code}: {msg}" where msg is the exchange-returned human-readable message from the JSON msg field -- not an API key or secret. The RawBody property (which carries the full response body and is the higher-risk field, per ExchangeApiException.RawBody) is NOT forwarded into ToolError. This is the safe design.
- Fix: none required
- Rule reference: none

### ParseSymbol FormatException message echoes user input
- Severity: LOW
- Confidence: 70
- Blocking: no
- File: src/CryptoExchanges.Net.Mcp/ToolInputs.cs:51
- Detail: The FormatException message includes got '{value}' where value is the raw agent-supplied string. This message propagates to ToolError.Message via the FormatException arm in ToolRunner. In an MCP context the agent already knows what it sent, so this is low risk. Asset.TryOf caps input at 32 characters and rejects non-alphanumeric characters before returning false, so the validation loop is bounded. Exhaustion is not feasible. The message echo is a minor concern, not a blocking one.
- Fix: Optionally clamp value in the format string to a safe display length (e.g. value.Length > 60 ? value[..60] + ... : value). Not required for approval.
- Rule reference: none

### OperationCanceledException re-throw through MCP boundary
- Severity: INFO
- Confidence: 95
- Blocking: no
- File: src/CryptoExchanges.Net.Mcp/ToolRunner.cs:98-101
- Detail: OperationCanceledException is explicitly re-thrown before the broad catch. This is the correct behavior: cancellation is a transport/lifecycle signal, not a business error, and the MCP host is responsible for handling it. Suppressing it inside ToolResult would cause the host to hang waiting for a response that signals success or failure when neither has occurred.
- Fix: none required
- Rule reference: none

### Static dictionaries thread-safety
- Severity: INFO
- Confidence: 99
- Blocking: no
- File: src/CryptoExchanges.Net.Mcp/ToolInputs.cs:9-34
- Detail: Exchanges and Intervals are private static readonly Dictionary<string, ...>. They are populated in field initializers (which run once during type initialization under the CLR static constructor lock) and are never mutated after that. All access is via TryGetValue, which is safe for concurrent reads on a Dictionary that is never written to after initialization. No thread-safety concern.
- Fix: none required
- Rule reference: none

### No credential material in scope
- Severity: INFO
- Confidence: 99
- Blocking: no
- File: all three new source files
- Detail: Searched for ApiKey, SecretKey, BINANCE_API, BINANCE_SECRET, Environment, GetEnvironmentVariable, Console., log and Log -- zero matches in source files. The new files are entirely data-shaping utilities with no access to credential configuration.
- Fix: none required
- Rule reference: none

### Broad catch(Exception) at MCP boundary -- acceptable design
- Severity: INFO
- Confidence: 95
- Blocking: no
- File: src/CryptoExchanges.Net.Mcp/ToolRunner.cs:103-107
- Detail: The catch-all is intentional and is documented inline with CA1031 suppression rationale. AuthenticationException is correctly mapped to the AuthRequired category before falling through to _ => Unknown, so authentication failures are NOT silently classified as Unknown. The inheritance chain (AuthenticationException derives from ExchangeApiException) is handled correctly because AuthenticationException and RateLimitExceededException arms appear before the ExchangeApiException arm in the switch expression, matching the inline comment at line 113.
- Fix: none required
- Rule reference: none
