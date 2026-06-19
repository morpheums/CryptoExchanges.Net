# API Review — TASK-039

## Verdict: APPROVED

## Findings

### Finding: `new Symbol(Asset.Btc, Asset.Usdt)` constructor call — accurate
- **Severity**: N/A
- **Confidence**: 100
- **File**: README.md:42
- **Verdict**: PASS
- `Symbol` is a `readonly record struct` with constructor `Symbol(Asset base, Asset quote)` — `src/CryptoExchanges.Net.Core/Models/Symbol.cs:18`. `Asset.Btc` and `Asset.Usdt` are public static readonly fields on `Asset`. The README snippet matches exactly.

### Finding: `exchange.MarketData.GetPriceAsync(...)` — accurate
- **Severity**: N/A
- **Confidence**: 100
- **File**: README.md:42
- **Verdict**: PASS
- `IExchangeClient.MarketData` is `IMarketDataService`; `GetPriceAsync(Symbol, CancellationToken = default)` returns `Task<decimal>` — `src/CryptoExchanges.Net.Core/Interfaces/IMarketDataService.cs:42`. The snippet (writing the result to a string) is consistent with a `decimal` return. No mismatch.

### Finding: `BinanceExchangeClient.Create(new BinanceOptions { ... })` pattern — accurate
- **Severity**: N/A
- **Confidence**: 100
- **File**: README.md:36-40
- **Verdict**: PASS
- `BinanceExchangeClient.Create(BinanceOptions)` is a public static factory — `src/CryptoExchanges.Net.Binance/BinanceExchangeClient.cs:68`. `BinanceOptions` has `ApiKey` and `SecretKey` properties. Pattern matches exactly.

### Finding: Package name `CryptoExchanges.Net.Binance` — accurate
- **Severity**: N/A
- **Confidence**: 100
- **File**: README.md:32, 15
- **Verdict**: PASS
- PackageId `CryptoExchanges.Net.Binance` matches the `.csproj`. All four supported-exchange package names in the table (`CryptoExchanges.Net.Binance`, `.Bybit`, `.Okx`, `.Bitget`) have corresponding `src/` projects.

### Finding: NuGet version badge `v0.2.0-preview.1` — accurate
- **Severity**: N/A
- **Confidence**: 100
- **File**: README.md:5
- **Verdict**: PASS
- `Directory.Build.props` sets `<Version>0.2.0-preview.1</Version>`. Badge is correct.

### Finding: MCP tool count "12 tools" — accurate
- **Severity**: N/A
- **Confidence**: 100
- **File**: README.md:60
- **Verdict**: PASS
- `MarketDataTools.cs` has 6 `[McpServerTool]` attributes; `AccountTools.cs` has 6 `[McpServerTool]` attributes. Total = 12. Split ("six market-data tools ... six account tools") is also correct.

### Finding: "REST, spot market data and account — read and write" caption accuracy
- **Severity**: MEDIUM
- **Confidence**: 85
- **File**: README.md:23
- **Verdict**: CONCERN (non-blocking by task scope, but warrants attention)
- **Issue**: The caption reads "read and write" which implies write/trading operations (order placement, cancellation). This is accurate for the library — `ITradingService` is part of the public contract and implementations do support `PlaceOrderAsync`, `CancelOrderAsync`, etc. However, the MCP server is explicitly read-only, and the README section immediately above this caption describes exchanges. A reader skimming the exchange table might associate "read and write" only with the MCP server context introduced moments earlier, creating potential confusion about MCP being read-only. The caption itself is factually correct for the library layer.
- **Fix**: No change required for accuracy; the library does support write operations. If clarity is desired, the wording could be tightened to "Library: REST, spot — market data, trading, and account (read + write). MCP server: read-only." but this is cosmetic.

### Finding: "Coming soon" exchanges (Coinbase/Kraken/KuCoin) in ExchangeId enum
- **Severity**: N/A
- **Confidence**: 100
- **File**: README.md:19-21
- **Verdict**: PASS
- `ExchangeId` enum contains `Coinbase`, `Kraken`, and `Kucoin` values — `src/CryptoExchanges.Net.Core/Enums/ExchangeId.cs:9,13,17`. These are enum stubs without shipping implementations, consistent with "coming soon" status.

### Finding: No public API surface changed — docs-only task
- **Severity**: N/A
- **Confidence**: 100
- **File**: diff.patch
- **Verdict**: PASS
- The diff touches only `README.md`. No interfaces, models, enums, `.csproj` files, or DI extension signatures were modified. All API compatibility checks are trivially satisfied.

## Score: 9.5/10

All API accuracy checks pass. The 0.5 deduction is for the minor "read and write" caption phrasing that could momentarily confuse readers about MCP scope — non-blocking, factually correct for the library layer.
