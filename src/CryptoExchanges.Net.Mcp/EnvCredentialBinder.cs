using CryptoExchanges.Net;

namespace CryptoExchanges.Net.Mcp;

/// <summary>Populates <see cref="CryptoExchangesOptions"/> from environment variables.</summary>
public static class EnvCredentialBinder
{
    /// <summary>Applies known per-exchange env vars to <paramref name="options"/> using <paramref name="getEnv"/>.</summary>
    public static void Apply(CryptoExchangesOptions options, Func<string, string?> getEnv)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(getEnv);

        options.BinanceApiKey = getEnv("BINANCE_API_KEY");
        options.BinanceSecretKey = getEnv("BINANCE_SECRET_KEY");
        options.BybitApiKey = getEnv("BYBIT_API_KEY");
        options.BybitSecretKey = getEnv("BYBIT_SECRET_KEY");
        options.OkxApiKey = getEnv("OKX_API_KEY");
        options.OkxSecretKey = getEnv("OKX_SECRET_KEY");
        options.OkxPassphrase = getEnv("OKX_PASSPHRASE");
        options.BitgetApiKey = getEnv("BITGET_API_KEY");
        options.BitgetSecretKey = getEnv("BITGET_SECRET_KEY");
        options.BitgetPassphrase = getEnv("BITGET_PASSPHRASE");
        options.KucoinApiKey = getEnv("KUCOIN_API_KEY");
        options.KucoinSecretKey = getEnv("KUCOIN_SECRET_KEY");
        options.KucoinPassphrase = getEnv("KUCOIN_PASSPHRASE");
        options.CoinbaseApiKey = getEnv("COINBASE_API_KEY");
        options.CoinbasePrivateKey = getEnv("COINBASE_PRIVATE_KEY");
    }
}
