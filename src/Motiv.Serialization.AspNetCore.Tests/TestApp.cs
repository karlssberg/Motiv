using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
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
        builder.Services
            .AddAuthentication(TestAuthHandler.Scheme)
            .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.Scheme, null);
        builder.Services.AddAuthorization();
        services?.Invoke(builder.Services);
        var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        app.MapMotivRules("/api/rules", registry, options, rules, endpointOptions);
        await app.StartAsync();
        return app;
    }
}
