using System.ComponentModel;
using System.Reflection;
using Xunit;
using AwesomeAssertions;
using ModelContextProtocol.Server;
using CryptoExchanges.Net.Mcp.Tools;

namespace CryptoExchanges.Net.Mcp.Tests.Unit;

public class ToolRosterTests
{
    private static IEnumerable<MethodInfo> ToolMethods() =>
        new[] { typeof(MarketDataTools), typeof(AccountTools) }
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static))
            .Where(m => m.GetCustomAttribute<McpServerToolAttribute>() is not null);

    [Fact]
    public void Exposes_AllTwelve_ReadOnlyTools()
        => ToolMethods().Should().HaveCount(12);

    [Fact]
    public void EveryTool_HasNonEmptyDescription()
    {
        foreach (var m in ToolMethods())
            m.GetCustomAttribute<DescriptionAttribute>()!.Description.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void NoTool_NameImpliesAWriteOperation()
    {
        var banned = new[] { "Place", "Cancel", "Create", "Submit", "Delete" };
        foreach (var m in ToolMethods())
            banned.Should().NotContain(b => m.Name.Contains(b, StringComparison.OrdinalIgnoreCase));
    }
}
