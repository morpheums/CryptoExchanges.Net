using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(args);

// Check default providers
var defaultProviderCount = builder.Services
    .Where(sd => sd.ServiceType == typeof(ILoggerProvider))
    .Count();
Console.WriteLine($"Default providers after CreateApplicationBuilder: {defaultProviderCount}");

// Now add console
builder.Logging.AddConsole(o => o.LogToStandardErrorThreshold = LogLevel.Trace);

var afterAddConsoleCount = builder.Services
    .Where(sd => sd.ServiceType == typeof(ILoggerProvider))
    .Count();
Console.WriteLine($"Providers after AddConsole: {afterAddConsoleCount}");

// List them
foreach (var sd in builder.Services.Where(sd => sd.ServiceType == typeof(ILoggerProvider)))
{
    Console.WriteLine($"  - {sd.ImplementationType?.Name}");
}
