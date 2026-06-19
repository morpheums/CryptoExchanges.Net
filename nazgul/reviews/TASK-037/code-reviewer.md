# Code Reviewer — TASK-037

## Verdict: CHANGES_REQUESTED

## Findings

### REJECT — Trade model: field documented as `TradeId`, actual name is `Id` (confidence: 100%)
`architecture.md` line 72, domain-models table:
```
| `Trade` | `TradeId`, `Price`, `Quantity`, `Timestamp`, `IsBuyerMaker` |
```
Source truth (`src/CryptoExchanges.Net.Core/Models/Trade.cs`):
```csharp
public sealed record Trade(
    Symbol Symbol,
    string? Id = null,
    ...
```
The field is `Id` (nullable string), not `TradeId`. A developer reading this table and writing `trade.TradeId` will get a compile error.

Fix: Change `TradeId` to `Id` in the `Trade` row of the domain-models table.

---

### REJECT — Order model: fields `Quantity` and `FilledQuantity` do not exist (confidence: 100%)
`architecture.md` line 73, domain-models table:
```
| `Order` | `OrderId`, `Symbol`, `Side`, `Type`, `Status`, `Price`, `Quantity`, `FilledQuantity`, `CreatedAt` |
```
Source truth (`src/CryptoExchanges.Net.Core/Models/Order.cs`):
```csharp
public sealed record Order(
    Symbol Symbol,
    string OrderId,
    ...
    decimal OriginalQuantity = 0,
    decimal ExecutedQuantity = 0,
    ...
```
Neither `Quantity` nor `FilledQuantity` exist. The correct names are `OriginalQuantity` and `ExecutedQuantity`. A developer reading this table and writing `order.Quantity` or `order.FilledQuantity` will get a compile error.

Fix: Replace `Quantity, FilledQuantity` with `OriginalQuantity, ExecutedQuantity` in the `Order` row.

---

### REJECT — Duplicate `client` variable in the same code block for all four exchange sections (confidence: 100%)
`exchanges.md`, Binance section (lines 27–39), Bybit (lines 70–81), OKX (lines 115–127), Bitget (lines 162–174).

Each exchange has a single fenced `csharp` block that declares `await using var client` twice — once for the options path and once for `CreateFromEnvironment()`. This does not compile; C# does not allow redeclaring a local in the same scope.

Example (Binance, lines 27–39):
```csharp
using CryptoExchanges.Net.Binance;

// From explicit options
await using var client = BinanceExchangeClient.Create(new BinanceOptions { ... });

// From environment variables (BINANCE_API_KEY / BINANCE_SECRET_KEY)
await using var client = BinanceExchangeClient.CreateFromEnvironment();   // ← CS0128
```

Fix: Split into two separate fenced code blocks — one per usage — or rename the second variable (e.g. `client2`, or use a descriptive name like `envClient`). Splitting into two blocks is the cleaner doc pattern. Apply the same fix to Bybit, OKX, and Bitget sections.

---

### REJECT — `mcp-server.md` linked from `library-usage.md` does not exist (confidence: 95%)
`library-usage.md` line 391 (Further reading):
```
- [MCP server](mcp-server.md) — AI-agent read-only access via the Model Context Protocol
```
`docs/mcp-server.md` does not exist in the repository. TASK-038 is described as the task that will create it, but this PR delivers the link before the target file exists.

A broken relative link in rendered markdown is a user-facing defect. This is a forward reference with no fallback.

Fix: Either remove the `mcp-server.md` bullet from this PR and re-add it in TASK-038 alongside the file, or add a clear inline note such as "(coming soon — TASK-038)" so readers understand why the link is intentionally not live.

---

### PASS — SVG asset paths in exchanges.md all resolve (confidence: 100%)
`docs/assets/exchanges/` contains `binance.svg`, `bybit.svg`, `okx.svg`, `bitget.svg`, `coinbase.svg`, `kraken.svg`, `kucoin.svg`. Every `<img src="assets/exchanges/...">` in `exchanges.md` matches an existing file.

---

### PASS — Cross-links between the four docs are consistent (confidence: 100%)
All relative links among `getting-started.md`, `library-usage.md`, `architecture.md`, and `exchanges.md` point to files that are delivered in this PR. Navigation is internally consistent.

---

### PASS — Opsec compliance (confidence: 100%)
No WebSockets, AI positioning, monetization, competitive analysis, or gateway content. "Coming soon" is limited to exchanges. No credentials or real keys are present anywhere in the docs.

---

### PASS — All other code samples match the verified API surface (confidence: 100%)
Every code sample in `getting-started.md` and `library-usage.md` that was listed in the ground-truth inventory is verified correct: constructors, method names, parameter names, return types, exception hierarchy, DI registration, KlineInterval values, and `PlaceOrderRequest` usage.

---

### CONCERN — `library-usage.md` uses `trade.Timestamp` in the public-trade loop but the `Trade.Timestamp` field is `DateTimeOffset?` (nullable) (confidence: 85%)
`library-usage.md` line 125:
```csharp
Console.WriteLine($"[{t.Timestamp:HH:mm:ss}] ...");
```
`Trade.Timestamp` is `DateTimeOffset?` (source: `Trade.cs` line 9). Formatting a nullable directly with `:HH:mm:ss` works at runtime (produces an empty string if null) but a reader may interpret the snippet as treating it as non-nullable. A more precise example would be `t.Timestamp?.ToString("HH:mm:ss") ?? "–"` or an explicit null-check. This is a documentation-accuracy concern, not a correctness error that causes a compile failure.

---

## Summary

Four blocking issues require fixes before approval:

1. `architecture.md` line 72: `Trade.TradeId` — field name is `Id`.
2. `architecture.md` line 73: `Order.Quantity`/`FilledQuantity` — correct names are `OriginalQuantity`/`ExecutedQuantity`.
3. `exchanges.md` (all four exchange sections): duplicate `var client` declaration inside a single code block — does not compile.
4. `library-usage.md` line 391: link to `mcp-server.md` which does not exist.

All other API signatures, method names, DI registrations, SVG asset paths, and internal cross-links are verified correct.
