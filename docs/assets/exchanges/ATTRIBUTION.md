# Exchange Icon Attribution

These icons appear in the supported-exchanges table to denote integrations.
The CryptoExchanges.Net project code is Apache-2.0 licensed; exchange names and
brand marks remain the property of their respective owners and are used here
solely to identify supported integrations, not to imply endorsement.

## Official icons (Simple Icons — CC0 1.0)

| File | Exchange | Theme | Source |
|------|----------|-------|--------|
| `binance.svg`  | Binance | fixed (brand gold)        | https://cdn.simpleicons.org/binance |
| `okx-light.svg` / `okx-dark.svg` | OKX | theme-aware (black/white) | https://cdn.simpleicons.org/okx |
| `kucoin.svg`   | KuCoin  | fixed (brand green)       | https://cdn.simpleicons.org/kucoin |

Simple Icons are released under the [CC0 1.0 Universal Public Domain
Dedication](https://creativecommons.org/publicdomain/zero/1.0/).
The OKX path is reused for both theme variants (black for light, white for dark).

## Recreated marks (original geometry — not in Simple Icons)

The following exchanges are **not** in the Simple Icons catalog. The icons here
are minimal, original geometric recreations built from basic SVG shapes to keep
the table visually consistent. They are simplified brand-style marks, not copies
of any vendor's logo file, and use no `<text>` elements (stripped by GitHub's
SVG sanitizer).

| File | Exchange | Treatment |
|------|----------|-----------|
| `coinbase.svg` | Coinbase | blue badge + white "C" ring, fixed both themes |
| `bitget.svg`   | Bitget   | teal badge + black double-chevron, fixed both themes |
| `kraken.svg`   | Kraken   | purple "jellyfish" mark (dome + tentacles), fixed both themes |
| `bybit-light.svg` / `bybit-dark.svg` | Bybit | theme-aware mark with orange accent |

The Bybit mark is a stand-in; replace `bybit-light.svg` / `bybit-dark.svg` with
the official Bybit wordmark SVG when available. Theme-aware files are wired in
the README via `<picture>` + `prefers-color-scheme`.
