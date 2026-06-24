# Document Manifest

## Generated Documents

| Document | Status | Generated At | Approved |
|----------|--------|-------------|----------|
| PRD-FEAT-009.md | generated | 2026-06-24 | pending |
| TRD-FEAT-009.md | generated | 2026-06-24 | pending |
| ADR-FEAT-009.md | generated | 2026-06-24 | pending |
| TEST-PLAN-FEAT-009.md | generated | 2026-06-24 | pending |

## Classification

- Type: BROWNFIELD
- Reasoning: Extending three existing exchange packages (Bybit, OKX, Bitget) with
  streaming capability by cloning the verified Binance/KuCoin pattern. Shared engine
  is not modified. No new projects required.

## Existing Documentation Referenced

| Existing Document | Referenced By | How Used |
|-------------------|--------------|----------|
| `src/CryptoExchanges.Net.Http/Streaming/StreamEngine.cs` | TRD, ADR | Binding constraints K1/K2/K3/C1 extracted verbatim |
| `src/CryptoExchanges.Net.Http/Streaming/IStreamProtocol.cs` | TRD | Variation-point interface documented |
| `src/CryptoExchanges.Net.Http/Streaming/StreamServiceRegistration.cs` | TRD | `AddStreams<TOptions>` body described |
| `src/CryptoExchanges.Net.Http/Streaming/StreamConnectionInfo.cs` | TRD, ADR | MinOutboundInterval and HeartbeatPolicy fields |
| `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamProtocol.cs` | TRD, ADR | Static-URL pattern, server-ping policy, batch builder |
| `src/CryptoExchanges.Net.Binance/Streaming/BinanceStreamDecoders.cs` | TRD | Decoder closure pattern documented |
| `src/CryptoExchanges.Net.Binance/StreamServiceCollectionExtensions.cs` | TRD | DI extension pattern |
| `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamProtocol.cs` | TRD, ADR | Client-ping pattern, JSON ping payload |
| `src/CryptoExchanges.Net.Kucoin/Streaming/KucoinStreamDecoders.cs` | TRD | Double-nested data unwrap pattern |
| `src/CryptoExchanges.Net.Kucoin/StreamServiceCollectionExtensions.cs` | TRD | DI extension for non-static-URL venue |
| `tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamProtocolTests.cs` | Test Plan | Protocol unit test structure |
| `tests/CryptoExchanges.Net.Binance.Tests.Unit/Streaming/BinanceStreamDiTests.cs` | Test Plan | DI wiring test structure |
| `tests/CryptoExchanges.Net.Binance.Tests.Integration/Streaming/BinanceStreamSmokeTests.cs` | Test Plan | Multi-symbol integration smoke test structure |
| `tests/CryptoExchanges.Net.Kucoin.Tests.Integration/KucoinStreamingSmokeTests.cs` | Test Plan | Integration test self-skip pattern |
| `src/CryptoExchanges.Net.Bybit/BybitOptions.cs` | TRD | Existing options structure for Bybit |
| `src/CryptoExchanges.Net.Okx/OkxOptions.cs` | TRD | Existing options structure for OKX |
| `src/CryptoExchanges.Net.Bitget/BitgetOptions.cs` | TRD | Existing options structure for Bitget |
