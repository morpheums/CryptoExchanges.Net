using CryptoExchanges.Net.Core.Exceptions;

namespace CryptoExchanges.Net.Mcp;

/// <summary>Runs a tool body and converts any exception into a structured <see cref="ToolError"/>.</summary>
public static class ToolRunner
{
    /// <summary>Executes <paramref name="action"/>, returning a success envelope or a mapped error envelope.</summary>
    public static async Task<ToolResult<T>> RunAsync<T>(Func<Task<T>> action)
    {
        ArgumentNullException.ThrowIfNull(action);
        try
        {
            return ToolResult<T>.Success(await action().ConfigureAwait(false));
        }
#pragma warning disable CA1031 // Intentional broad catch: ToolRunner is the MCP boundary — nothing may escape.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            return ToolResult<T>.Failure(new ToolError(Categorize(ex), ex.Message));
        }
    }

    private static string Categorize(Exception ex) => ex switch
    {
        // NOTE: specific arms must precede ExchangeApiException because
        // RateLimitExceededException and AuthenticationException derive from it.
        AuthenticationException => "AuthRequired",
        RateLimitExceededException => "RateLimited",
        ExchangeNotRegisteredException => "ExchangeUnavailable",
        ExchangeConnectivityException => "Connectivity",
        FormatException => "SymbolNotSupported",
        ExchangeApiException => "ExchangeError",
        _ => "Unknown",
    };
}
