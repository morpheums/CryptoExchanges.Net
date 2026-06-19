# Code Review — TASK-030
**Reviewer**: code-reviewer
**Date**: 2026-06-19
**Verdict**: APPROVED

## Summary
`MarketDataTools.cs` introduces six MCP tool methods backed by a shared `Resolve<T>` helper. Guards, routing logic, and error-path categorization are all correct. The build is clean at 0W/0E under `TreatWarningsAsErrors`. Test coverage for the four untested tools (`GetTicker`, `GetOrderBook`, `GetRecentTrades`, `GetExchangeInfo`) is thin, but these tools all run through `ToolRunner.RunAsync` whose exception-categorization is thoroughly covered in `ToolPrimitivesTests`. One test name is misleading but the assertion is correct.

## Findings

### PASS Null-guard consistency (confidence: 99%)
Every public tool method guards `factory` with `ArgumentNullException.ThrowIfNull` and every non-nullable string parameter with `ArgumentException.ThrowIfNullOrWhiteSpace`. `GetTicker`'s optional `symbol?` is correctly not guarded (it is nullable by design). Rule reference: LR-001 / `SymbolMapper.cs:27`.

### PASS Routing and error-path correctness (confidence: 99%)
All six tools short-circuit to `ToolResult.Failure(Unavailable(exchange))` when `TryParseExchange` or `TryGet` fails. `GetKlines` adds a second short-circuit for `TryParseInterval` returning `"BadInterval"`. The `Resolve<T>` helper is called only after guards pass, is private, and is not responsible for guarding (callers already guard). The `client!` null-forgiving on lines 40, 77, 103, and 116 is safe: each is guarded by a `TryGet` returning `true`, which is annotated `[NotNullWhen(true)]` on `IExchangeClientFactory`.

### PASS Resolve<T> helper completeness (confidence: 98%)
`Resolve<T>` correctly handles the exchange/client lookup, delegates symbol parsing inside the `ToolRunner.RunAsync` lambda (so a `FormatException` from `ParseSymbol` is caught and mapped to `"SymbolNotSupported"`), and threads the `CancellationToken` through the `Func` signature. The `GetTicker` inline expansion is intentional and documented in the task: `Resolve<T>` requires a symbol, `GetTicker` does not.

### PASS CancellationToken design (confidence: 95%)
None of the tool entry points accept `CancellationToken` — this is consistent with the MCP stdio transport model (the process-level shutdown is the cancellation surface). `ToolRunner.RunAsync` re-throws `OperationCanceledException`, preserving cooperative cancellation from the service layer. Passing `default` at each call site is the established project pattern for this layer.

### PASS Description quality — LLM-facing strings (confidence: 96%)
All six `[Description(...)]` strings are concise, informative, and correctly formatted. The shared constants `ExchangeParam` and `SymbolParam` avoid duplication. `GetTicker`'s "Omit for all pairs." extension is correct. The interval description enumerates all accepted values, which is useful for an LLM selecting inputs.

### PASS Build and analyzer compliance (confidence: 100%)
`dotnet build src/CryptoExchanges.Net.Mcp/CryptoExchanges.Net.Mcp.csproj --configuration Release` produces 0 warnings, 0 errors. `GenerateDocumentationFile` is `false` for this executable project (line 7 of `.csproj`), so XML doc requirements do not apply. The `CA1515` suppression is pre-existing, justified in a comment, and not expanded by this diff.

### PASS All 29 unit tests pass (confidence: 100%)
`dotnet test tests/CryptoExchanges.Net.Mcp.Tests.Unit/` passes 29/29. The 4 new tests in `MarketDataToolsTests.cs` all pass.

### CONCERN Missing happy-path tests for GetTicker, GetOrderBook, GetRecentTrades, GetExchangeInfo (confidence: 75%)
Four of the six tools have zero test coverage in `MarketDataToolsTests.cs`. Each has a unique routing path: `GetTicker` has the optional-symbol branch (null vs. non-null), `GetOrderBook` and `GetRecentTrades` use `Resolve<T>` (their happy path is structurally identical to `GetPrice`, which is tested), `GetExchangeInfo` is a no-symbol variant. The `ToolRunner`'s exception mapping is already exhaustively tested in `ToolPrimitivesTests`, so the risk is low — but the optional-symbol branch in `GetTicker` (symbol == null → all-ticker path, symbol != null → single-ticker path) is a behaviorally distinct path with no test. LR-005 scope is `src/**/Services/*.cs` so this is non-blocking, but a test for the `GetTicker` null-symbol path would improve confidence. This is a CONCERN, not a REJECT.

### CONCERN Misleading test name on GetKlines bad-interval case (confidence: 90%)
`MarketDataToolsTests.cs:55` — the method is named `GetKlines_BadInterval_ReturnsSymbolNotSupported_OrValidationError` but the assertion on line 61 asserts strictly `Category.Should().Be("BadInterval")`. There is no code path that returns `"SymbolNotSupported"` for this input; the `"_OrValidationError"` suffix is a naming artifact that does not reflect the actual assertion. The test is correct; the name is misleading. Rename to `GetKlines_BadInterval_ReturnsBadInterval` for clarity.

## Verdict rationale
Both blocking criteria (HIGH/MEDIUM finding with confidence >= 80%) are not met. The null-guard pattern is correctly applied, all routing paths return the right `ToolError` category, the `Resolve<T>` helper is complete and correct, and the build is clean. The two findings are CONCERNs (confidence < 80% for the coverage gap due to mitigating coverage in `ToolPrimitivesTests`; the misleading test name is style-only). No blocking defects.
