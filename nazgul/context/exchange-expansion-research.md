# Exchange Expansion — Research & Decision

**Date:** 2026-06-17
**Decision:** Add three new exchanges in priority order: **Bybit → OKX → Bitget**.
**Market scope:** Global (NOT US-specific) — optimize for volume + signing reuse, not US licensing.

Source: deep-research run (106 agents, 24 sources, 23/25 claims adversarially confirmed).

## Why these three, in this order

All three reuse the existing `HMACSHA256` primitive in
`src/CryptoExchanges.Net.Binance/Auth/BinanceSignatureService.cs` (the baseline:
HMAC-SHA256 with **hex** output over a query string). The porting work is per-exchange
sign-string assembly, encoding, and credential handling — NOT a new crypto primitive.

| # | Exchange | Volume (2025–26) | Auth | Delta vs Binance HMAC |
|---|----------|------------------|------|------------------------|
| 1 | **Bybit** | #2 spot ~8.1%, #3 derivatives ~$1.49T/qtr | HMAC-SHA256 (hex) | Sign-string builder only: `timestamp+apiKey+recvWindow+queryString` (GET) / `+jsonBody` (POST). Lowest effort. |
| 2 | **OKX** | #2 derivatives ~$2.19T/qtr, top-7 spot | HMAC-SHA256 (**base64**) | Composite prehash `timestamp+METHOD+requestPath+body`; **base64** not hex; **3rd credential (passphrase)** + `OK-ACCESS-KEY/SIGN/TIMESTAMP/PASSPHRASE` headers. Forces the signer generalization. |
| 3 | **Bitget** | top-5/6 spot ~6.4% & derivatives | HMAC-SHA256 (base64) | Prehash `timestamp+UPPERCASE-method+requestPath+'?'+queryString+body`; base64; `ACCESS-PASSPHRASE` header. Slots into the OKX-shaped abstraction. |

## Architectural implication

The **signature abstraction should generalize after Bybit, against OKX**. Plan sequence:
1. **Bybit** — minimal new abstraction; proves the `Core→Http→Exchange→DI` layering generalizes
   beyond one exchange with the HMAC primitive intact.
2. **OKX** — drives the real generalization: pluggable per-exchange sign-string builder,
   base64-vs-hex output, an optional **passphrase** credential, and a header set distinct from
   Binance's query-string-appended signature.
3. **Bitget** — validates the OKX-era abstraction holds for a third exchange with minimal new code.

## Deprioritized (with reason)
- **Coinbase** — strongest US standing, but current auth (post-Feb-2025) is **asymmetric
  per-request JWT** (Ed25519 recommended / ES256 legacy, 2-min expiry, `Authorization: Bearer`).
  Structurally incompatible with the HMAC signer; would need a whole new asymmetric auth path.
- **Kraken** — US-centric/reputable but a different scheme; under-analyzed.
- **Crypto.com / Gate.io / KuCoin / MEXC / HTX** — viable later HMAC-SHA256 additions
  (KuCoin heaviest: 5 headers + HMAC-encrypted passphrase).

## Caveats
- Volume figures are methodology-dependent (CoinGecko vs CoinGlass disagree on exact ranks;
  both agree Binance dominates ~34–37%). Relative tiers are robust; exact percentages are not.
- US-accessibility dimension was under-sourced; decision assumes a **global** audience.
- Each exchange ships a proven JKorf .NET reference (Bybit.Net, OKX.Net, Bitget.Net) confirming
  .NET tractability — useful as a sanity reference, not for code reuse.
