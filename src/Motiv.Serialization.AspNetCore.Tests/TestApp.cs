using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>Spins up an in-memory host that mounts the rules endpoints under /api/rules.</summary>
internal static class TestApp
{
    public static async Task<WebApplication> StartAsync(
        SpecRegistry registry,
        MotivRulesOptions options,
        RuleSet? rules = null,
        Action<MotivRulesEndpointOptions>? endpointOptions = null,
        Action<IServiceCollection>? services = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestAuth();
        services?.Invoke(builder.Services);
        var app = builder.Build();
        app.UseTestAuth();
        app.MapMotivRules("/api/rules", registry, options, rules, endpointOptions);
        await app.StartAsync();
        return app;
    }
}
