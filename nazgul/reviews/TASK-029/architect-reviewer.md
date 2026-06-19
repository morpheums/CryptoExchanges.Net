# Architect Review — TASK-029

## Verdict: APPROVED
Confidence: 97

## Summary
The three primitives (ToolResult, ToolInputs, ToolRunner) are correctly layered leaf utilities: they reference only Core enums, models, and exceptions — no Http, Exchange, or DI namespaces appear anywhere. The error category seam covers the full specified set of 7 categories with correct switch-arm ordering (specific subtypes before ExchangeApiException base). OperationCanceledException is correctly re-thrown, not caught. All 24 tests pass with zero warnings and zero errors under TreatWarningsAsErrors. One non-blocking concern is raised about two Core exception subtypes (InsufficientBalanceException, InvalidOrderException) that currently fall through to "Unknown" rather than "ExchangeError" — a minor seam incompleteness, not a blocking defect.

## Findings

### Layering — no violations
- Severity: INFO
- Confidence: 99
- Blocking: no
- File: src/CryptoExchanges.Net.Mcp/ToolInputs.cs, ToolResult.cs, ToolRunner.cs
- Detail: ToolInputs.cs imports only CryptoExchanges.Net.Core.Enums and CryptoExchanges.Net.Core.Models. ToolRunner.cs imports only CryptoExchanges.Net.Core.Exceptions. ToolResult.cs has no using directives at all. No Http, Binance, Bybit, Okx, Bitget, or DI namespaces are referenced in the three primitives. The MCP host project (CryptoExchanges.Net.Mcp.csproj) does reference Exchange and DI assemblies, but that is expected for the host layer; these three files themselves are clean of upward dependencies.
- Fix: none required
- Rule reference: none

### Error category seam — InsufficientBalanceException and InvalidOrderException fall to "Unknown"
- Severity: LOW
- Confidence: 85
- Blocking: no
- File: src/CryptoExchanges.Net.Mcp/ToolRunner.cs, lines 28–39
- Detail: The Core hierarchy has two additional ExchangeApiException subclasses: InsufficientBalanceException and InvalidOrderException. Both are sealed and derive from ExchangeApiException. With the current Categorize switch, both fall through the ExchangeApiException arm and get "ExchangeError" — which is correct at runtime because ExchangeApiException IS matched before the wildcard. Re-reading: the switch arm `ExchangeApiException => "ExchangeError"` will match both subtypes because pattern matching on type checks inheritance. This is correct behavior. The concern is that the comment "specific arms must precede ExchangeApiException" documents only RateLimitExceededException and AuthenticationException; InsufficientBalanceException and InvalidOrderException ARE covered by the base arm and produce "ExchangeError", which is the right category. No defect.
- Fix: none required; confirmed correct by inheritance-based pattern matching semantics
- Rule reference: none

### ToolInputs — static readonly dictionaries are module-init state, not mutable global state
- Severity: INFO
- Confidence: 99
- Blocking: no
- File: src/CryptoExchanges.Net.Mcp/ToolInputs.cs, lines 9–28
- Detail: The two static readonly dictionaries are initialized once at class initialization and are never mutated. Thread-safety is guaranteed by the readonly modifier and Dictionary's read-concurrency safety. This is the correct pattern for an immutable lookup table.
- Fix: none required
- Rule reference: none

### LR-001 compliance — ParseSymbol guard
- Severity: INFO
- Confidence: 99
- Blocking: no
- File: src/CryptoExchanges.Net.Mcp/ToolInputs.cs, line 48
- Detail: ParseSymbol correctly uses ArgumentException.ThrowIfNullOrWhiteSpace(value) on the non-nullable string parameter. TryParseExchange and TryParseInterval accept string? and use the correct `value ?? string.Empty` guard before passing to Dictionary.TryGetValue, avoiding a NullReferenceException while correctly returning false for null input.
- Fix: none required
- Rule reference: LR-001

### OperationCanceledException boundary hygiene
- Severity: INFO
- Confidence: 99
- Blocking: no
- File: src/CryptoExchanges.Net.Mcp/ToolRunner.cs, lines 16–19
- Detail: OperationCanceledException is caught and immediately re-thrown before the broad catch. This is correct: cancellation is a host/infrastructure signal, not a tool error, and must propagate to the MCP runtime so it can honor cancellation tokens from the client. The broad catch with CA1031 suppression is appropriately scoped and documented.
- Fix: none required
- Rule reference: none

### No Core modifications
- Severity: INFO
- Confidence: 99
- Blocking: no
- File: diff.patch (entire patch)
- Detail: The diff introduces only new files in src/CryptoExchanges.Net.Mcp/ and tests/CryptoExchanges.Net.Mcp.Tests.Unit/. No files under src/CryptoExchanges.Net.Core/ are touched.
- Fix: none required
- Rule reference: none

### CA1000 suppression — justified
- Severity: INFO
- Confidence: 95
- Blocking: no
- File: src/CryptoExchanges.Net.Mcp/ToolResult.cs, lines 10 and 15
- Detail: CA1000 warns against static members on generic types. The suppression is justified here: Success and Failure are factory methods on ToolResult<T> that must know T to construct the instance, making them legitimate static members on a generic type. The suppression is tightly scoped (not project-wide) with an explanatory comment.
- Fix: none required
- Rule reference: none

### CONCERN — FormatException as SymbolNotSupported is a broad mapping
- Severity: LOW
- Confidence: 70
- Blocking: no
- File: src/CryptoExchanges.Net.Mcp/ToolRunner.cs, line 118
- Detail: Any FormatException — not just those from ParseSymbol — will be categorized as "SymbolNotSupported". If future tool implementations throw FormatException for non-symbol reasons (e.g., malformed quantity parsing), the category will be misleading to AI consumers. A custom exception type (e.g., SymbolFormatException : FormatException) would make the seam more precise without breaking callers. This is a pre-existing design choice and not a blocking defect at this task's scope.
- Fix: Consider introducing a SymbolFormatException : FormatException in Core and catching that type specifically in Categorize. Non-blocking; document the limitation in a code comment.
- Rule reference: none
