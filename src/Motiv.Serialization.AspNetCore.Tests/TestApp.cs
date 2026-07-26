using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>Spins up an in-memory host that mounts the rules endpoints under /api/rules.</summary>
internal static class TestApp
{
    public static async Task<WebApplication> StartAsync(SpecRegistry registry, MotivRulesOptions options, RuleSet? rules = null)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        var app = builder.Build();
        app.MapMotivRules("/api/rules", registry, options, rules);
        await app.StartAsync();
        return app;
    }
}
