namespace CryptoExchanges.Net.Mcp;

/// <summary>A structured, agent-legible error returned by a tool.</summary>
public sealed record ToolError(string Category, string Message);
