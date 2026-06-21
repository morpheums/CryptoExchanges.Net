# Proposed Learned Rules (awaiting approval)

## CANDIDATE: Validate all public configurable options are actually consumed by the runtime factory
- **Scope-Agents**: implementer, api-reviewer
- **Scope-Globs**: src/**/StreamServiceCollectionExtensions.cs, src/**/ServiceCollectionExtensions.cs, src/**/*Options.cs
- **Confidence**: 92
- **Evidence**: TASK-062 (api-reviewer REJECT@98 — KucoinStreamOptions.RestBaseUrl public option never read by protocolFactory, silent no-op); Code-reviewer notes in prior exchanges (Binance pattern acknowledged but not flagged as blocking)
- **Dedup**: new

When a new exchange adds public configurable options (`*Options` class with settable properties), every option must demonstrably control runtime behavior in the DI factory (`AddXxxStreams`, `AddXxxExchange`). Registering an option via `ValidateOnStart` but not consuming it in the factory creates a misleading public API surface — users can set the property and get a silent no-op instead of an error or the intended effect. The fix is to resolve the `*Options` instance in the factory lambda (e.g., `protocolFactory`), apply explicit guards (LR-001: `ArgumentException.ThrowIfNullOrWhiteSpace` for string options), validate URI shape if applicable, and thread the validated value through to the production objects. Add a unit test proving a custom option value is the actual runtime value used (e.g., `AddKucoinStreams_CustomRestBaseUrl_IsHostUsedForBulletPublicNegotiation` in TASK-062 Cycle 2).

---

## CANDIDATE: Wire all new exchange keys through every MCP registration point in the same cycle
- **Scope-Agents**: implementer, api-reviewer, code-reviewer
- **Scope-Globs**: src/CryptoExchanges.Net.Mcp/ToolInputs.cs, src/CryptoExchanges.Net.Mcp/EnvCredentialBinder.cs, src/CryptoExchanges.Net.Mcp/Program.cs, docs/**
- **Confidence**: 88
- **Evidence**: TASK-060 (shipped AddKucoinExchange DI + claimed MCP wiring, but ToolInputs.cs and EnvCredentialBinder.cs were not updated); TASK-064 (discovered gaps: architect-reviewer Finding 2 notes ToolInputs + EnvCredentialBinder missing kucoin routing; also src/CryptoExchanges.Net.Mcp/README.md stale with only 4 exchanges)
- **Dedup**: new

When a new exchange is added (e.g., KuCoin), the implementer must update ALL of the following on the SAME task cycle to wire up the MCP host:

1. **src/CryptoExchanges.Net.Mcp/ToolInputs.cs** — add the exchange key mapping to `ExchangeId` (e.g., `["kucoin"] = ExchangeId.Kucoin`) in the exchange-routing dictionary.
2. **src/CryptoExchanges.Net.Mcp/EnvCredentialBinder.cs** — add the `*_API_KEY`, `*_SECRET_KEY`, and any exchange-specific env vars (e.g., `KUCOIN_PASSPHRASE`) to the credential-binding switch statement.
3. **src/CryptoExchanges.Net.Mcp/README.md** — update the list of supported exchanges in the package README (not just external docs).
4. **docs/mcp-server.md** — update the supported-exchanges list so it stays in sync with the package README.

Failure to complete all four creates a gap where the DI wiring succeeds but MCP tool invocation silently routes to the wrong exchange or fails with "unknown exchange". The pattern: each new exchange requires touching these four locations as a checklist, ideally in the same task.

---

## CANDIDATE: Use dependency injection interfaces for all service constructor parameters
- **Scope-Agents**: implementer, architect-reviewer, code-reviewer
- **Scope-Globs**: src/**/Resilience/*.cs, src/**/Services/*.cs
- **Confidence**: 85
- **Evidence**: TASK-057 (architect-reviewer REJECT@95 + api-reviewer REJECT@95 — KucoinSigningHandler constructor took concrete KucoinSignatureService instead of IKucoinSignatureService, violating DIP Invariant 11); Root cause: `SignPassphrase` method was not on the base `ISignatureService` interface
- **Dedup**: strengthens LR-001 (broader guard rule) — this is a specific DIP enforcement for service/handler constructors

Handler and service classes must accept interface types (not concrete types) for all dependencies that are provided by the DI container, even if the interface must be newly created to accommodate exchange-specific methods. If an exchange requires a capability not on the shared `ISignatureService` (e.g., KuCoin's `SignPassphrase(string)`), create a narrow exchange-specific interface extending the base (`internal interface IKucoinSignatureService : ISignatureService { string SignPassphrase(string); }`) and wire both the base and the extension interface. This ensures the handler is testable via mocks and complies with the Liskov Substitution Principle. Concrete type injection is only acceptable for static pure-function holders (`KucoinValueParsers`, `FormatTimestamp` static helpers).

---

## CANDIDATE: Ensure documented features in public changelog/README are wired in all required registration points
- **Scope-Agents**: code-reviewer, api-reviewer, architect-reviewer
- **Scope-Globs**: README.md, docs/**, src/CryptoExchanges.Net.Mcp/**
- **Confidence**: 80
- **Evidence**: TASK-064 (docs advertised `kucoin` MCP exchange key support in README and docs/mcp-server.md; architect-reviewer Finding 2 found src/CryptoExchanges.Net.Mcp/README.md package-level README still listed only 4 exchanges, creating stale documentation that contradicts public docs and misleads NuGet package consumers)
- **Dedup**: new

When updating README, docs, or public-facing materials to announce a new feature (e.g., "KuCoin is now supported in MCP"), audit all documentation sources (external docs, package-level READMEs, badges, counts) and update them in the same PR. In particular, ensure source-of-truth files (e.g., `src/CryptoExchanges.Net.Mcp/README.md` for the MCP package) stay in sync with derived documentation (e.g., `docs/mcp-server.md`). A mismatch where the package README lists 4 exchanges and the public docs list 5 creates confusion for consumers of the NuGet package. If a feature is announced in public docs, verify that all wiring points (DI, routing, credential binding, README, count) are complete before marking the task as done.
