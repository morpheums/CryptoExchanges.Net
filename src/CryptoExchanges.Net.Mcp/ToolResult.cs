namespace CryptoExchanges.Net.Mcp;

/// <summary>A structured, agent-legible error returned by a tool.</summary>
public sealed record ToolError(string Category, string Message);

/// <summary>Envelope returned by every tool so failures never throw across the MCP boundary.</summary>
public sealed record ToolResult<T>(bool Ok, T? Data, ToolError? Error)
{
    /// <summary>A successful result carrying <paramref name="data"/>.</summary>
#pragma warning disable CA1000 // Factory methods on generic types are the idiomatic C# pattern here.
    public static ToolResult<T> Success(T data) => new(true, data, null);

    /// <summary>A failed result carrying <paramref name="error"/>.</summary>
    public static ToolResult<T> Failure(ToolError error) => new(false, default, error);
#pragma warning restore CA1000
}
