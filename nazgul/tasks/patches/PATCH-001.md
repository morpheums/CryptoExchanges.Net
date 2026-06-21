# PATCH-001: Add NuGet download-count badges to README.md

## Metadata
- **Status**: DONE
- **Created**: 2026-06-21T22:11:49Z
- **Source**: /nazgul:patch
- **Flags**: none

## Description
Add NuGet download-count badges to README.md: (1) a downloads badge in the header badge row immediately after the ".NET 10.0" badge, using https://img.shields.io/nuget/dt/CryptoExchanges.Net.svg linking to the package; (2) a new "Downloads" column in the Supported Exchanges table, placed after the "Package" column, with a per-package downloads badge (https://img.shields.io/nuget/dt/CryptoExchanges.Net.<Exchange>?logo=nuget&label=downloads) for each Supported exchange (Binance, Bybit, OKX, Bitget, KuCoin) and "—" for the Coming soon rows (Coinbase, Kraken). Match the existing shields.io badge style. README.md only; no source/behavior change.

## Subtasks
1. Add header downloads badge after the .NET 10.0 badge linking to the CryptoExchanges.Net package.
2. Add a "Downloads" column to the Supported Exchanges table after "Package", with per-package downloads badges for Supported rows and "—" for Coming soon rows.

## Implementation Log
- Both subtasks applied to README.md.
