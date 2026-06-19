# Architect Reviewer — TASK-037

## Verdict: CHANGES_REQUESTED

## Findings

### REJECT: `IExchangeClientFactory` mis-attributed to the DI layer in the layer diagram (confidence: 92%)

The layer diagram (architecture.md, line 43) places `IExchangeClientFactory` inside the `CryptoExchanges.Net.DependencyInjection` box. This is wrong in two orthogonal ways:

1. The **interface** `IExchangeClientFactory` lives in `src/CryptoExchanges.Net.Core/Interfaces/IExchangeClientFactory.cs` — it is a Core contract, not a DI-layer artifact.
2. The **implementation** `ExchangeClientFactory` lives in `src/CryptoExchanges.Net.Http/ExchangeClientFactory.cs` (namespace `CryptoExchanges.Net.Http`, registered via `ExchangeServiceRegistration.TryAddSingleton` in the Http layer). It does not live in `CryptoExchanges.Net.DependencyInjection` at all.

The diagram also lists `AddBinanceExchange() · …` inside the DI box (line 36). Per ADR-001 and the actual source, `AddBinanceExchange()` is defined in `CryptoExchanges.Net.Binance/ServiceCollectionExtensions.cs`, not in the DI aggregation package. The diagram collapses both the aggregator and the per-exchange registrations into one box, creating the false impression that the DI package owns them.

The prose section "Dependency injection" (lines 188–198) is accurate — it correctly says per-exchange methods "are defined in the exchange's package, per ADR-001". The diagram contradicts the prose.

File: `docs/architecture.md`, lines 36–44

Fix:
- Move `IExchangeClientFactory` out of the DI box and into the Core box in the layer diagram (or add a note: "interface in Core, impl in Http").
- Change the DI box label from `AddCryptoExchanges() · AddBinanceExchange() · …` to `AddCryptoExchanges()` (the only method actually in the DI package). Add a note that `AddBinanceExchange()` etc. live in their respective exchange packages.

---

### REJECT: Broken internal link — `mcp-server.md` does not exist (confidence: 95%)

`docs/library-usage.md`, line 391 contains:

```
- [MCP server](mcp-server.md) — AI-agent read-only access via the Model Context Protocol
```

There is no `docs/mcp-server.md` file. The MCP project exists at `src/CryptoExchanges.Net.Mcp/` and has its own `README.md`, but no published doc page was added in this task or in any prior commit under `docs/`. This is a dangling link that will 404 in any rendered documentation site.

File: `docs/library-usage.md`, line 391

Fix: Either remove the bullet point entirely, or create `docs/mcp-server.md` in a follow-up task before this doc set goes live. Do not leave a broken link in the shipped docs.

---

### CONCERN: Handler chain direction label is inverted (confidence: 78%)

The handler chain diagram (architecture.md, lines 104–114) is headed "innermost → outermost" but the `↑` arrows point upward from `ErrorTranslationHandler` through to `RateLimitThrottleHandler`. In `HttpClientPipelineBuilder.Build()`, the construction order is:

```
throttle → exhaustion → Polly(resilience) → [signingHandler] → errorTranslation → innerHandler
```

outermost = `RateLimitThrottleHandler` (first handler that sees the request), innermost = `ErrorTranslationHandler` → raw transport. The arrows in the diagram point `ErrorTranslationHandler ↑ BinanceSigningHandler ↑ …` which, read bottom-up, shows the request path correctly. However, calling this "innermost → outermost" while the list is ordered innermost-at-top is the opposite of the customary "outermost first" table layout most readers expect.

The architecture-map.md ground truth (line 96) labels the identical chain as flowing from `RateLimitThrottleHandler` at the top (outermost/first-to-receive) down to `SocketsHttpHandler` at the bottom — the opposite ordering.

This is sub-blocking because a careful reader following the arrows gets the right answer, but the heading "innermost → outermost" paired with a top-to-bottom listing where innermost is at the top is confusing.

File: `docs/architecture.md`, lines 104–114

Fix: Either flip the diagram so `RateLimitThrottleHandler` is at top (outermost) and arrows point downward, matching architecture-map.md; or change the heading to "innermost → outermost (bottom to top)".

---

### PASS: `HttpClientPipelineBuilder.Build()` — exists and matches description (confidence: 100%)

`src/CryptoExchanges.Net.Http/HttpClientPipelineBuilder.cs` is a `public static class` with a `public static HttpClient Build(...)` method. The description at architecture.md line 110-111 is accurate.

---

### PASS: `*ClientComposer` pattern — exists for all four exchanges (confidence: 100%)

`BinanceClientComposer`, `BybitClientComposer`, `OkxClientComposer`, `BitgetClientComposer` all exist in their respective `Internal/` directories. Each implements both `Create()` (factory-free) and `ComposeForDi()` (DI) paths. The per-exchange table row (architecture.md line 138) is accurate.

---

### PASS: Handler chain — `BinanceSigningHandler` with "per-exchange impl" annotation (confidence: 100%)

The diagram at architecture.md line 107 shows `BinanceSigningHandler — adds timestamp + HMAC-SHA256 signature (per-exchange impl)`. Four separate signing handlers exist (`BinanceSigningHandler`, `BybitSigningHandler`, `OkxSigningHandler`, `BitgetSigningHandler`). Using Binance as the representative with an explicit "(per-exchange impl)" annotation is accurate.

---

### PASS: Signing algorithm claims (confidence: 100%)

- Binance/Bybit: HMAC-SHA256 — confirmed via `BinanceSignatureService.cs`, `BybitSignatureService.cs`.
- OKX: HMAC-SHA256 + ISO-8601 timestamp + passphrase header — confirmed by `OkxSignatureService.cs`.
- Bitget: HMAC-SHA256 + Unix timestamp + passphrase header — confirmed by `BitgetSignatureService.cs`.
- exchanges.md and architecture.md prose both correctly describe these distinctions.

---

### PASS: Layer dependency direction (confidence: 100%)

Core has zero production project references. Http depends only on Core. Per-exchange packages depend on Core + Http. DependencyInjection aggregates all. This matches the actual `.csproj` dependency graph and the layer diagram.

---

### PASS: `IExchangeClientFactory` is correctly described in the Core interfaces table (confidence: 100%)

architecture.md line 62 lists `IExchangeClientFactory` in the Core interfaces table with the correct description. Only the layer *diagram* misplaces it — the prose table is correct.

---

### PASS: "Coming soon" roadmap claim is accurate (confidence: 100%)

exchanges.md lists Coinbase, Kraken, and KuCoin as "coming soon" and states "These exchange IDs are present in the ExchangeId enum but are not yet implemented." The `ExchangeId` enum in `src/CryptoExchanges.Net.Core/Enums/ExchangeId.cs` confirms all three are present. No shipping implementation exists for any of them. No WebSocket, gateway, or monetization claims appear in the docs. The "coming soon" section is accurate and scoped.

---

### PASS: Per-exchange `Add*Exchange()` location described correctly in prose (confidence: 100%)

architecture.md prose (line 192) says `AddCryptoExchanges()` "delegates to each exchange's own `Add*Exchange()` method (defined in the exchange's package, per ADR-001)". This is accurate. The issue is confined to the layer diagram (covered under the REJECT finding above).

---

### PASS: All other internal links resolve (confidence: 100%)

- `architecture.md` → `getting-started.md`, `library-usage.md`, `exchanges.md` — all three files exist.
- `getting-started.md` → `library-usage.md`, `exchanges.md`, `architecture.md` — all exist.
- `exchanges.md` → `getting-started.md`, `library-usage.md`, `architecture.md` — all exist.
- `library-usage.md` → `getting-started.md`, `architecture.md`, `exchanges.md` — all exist.
- The broken link is only `mcp-server.md` (covered under its own REJECT finding).

---

## Summary

Two blocking issues. First, the layer diagram in `architecture.md` mis-places `IExchangeClientFactory` inside the DI box and lists `AddBinanceExchange()` there too; the interface actually lives in Core, the implementation in Http, and the per-exchange registration methods live in each exchange package. Second, `library-usage.md` links to `mcp-server.md` which does not exist in `docs/`, producing a dangling reference. Both are straightforward text fixes; no source code changes are needed.
