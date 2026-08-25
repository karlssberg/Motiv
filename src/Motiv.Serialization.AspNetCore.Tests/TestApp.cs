using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

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

    private sealed record Customer(bool IsActive);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class SampleRule() : Rule<Customer, string>("sample", IsActive);

    /// <summary>
    /// Builds and starts a DI-wired host — via
    /// <see cref="MotivRulesServiceCollectionExtensions.AddMotivRules"/> — with one registered spec
    /// (<c>customer.is-active</c>) and one enrolled rule (<c>sample</c>), for tests that need
    /// <see cref="MotivRulesBuilder"/> features unreachable from the plain <see cref="StartAsync"/>
    /// overload, such as <see cref="MotivRulesBuilder.AddRuleStore"/>.
    /// </summary>
    /// <remarks>
    /// Synchronous by design, and starts the host inline (blocking on
    /// <see cref="HostingAbstractionsHostExtensions.Start"/>) rather than returning a
    /// <see cref="Task"/>, so that a startup failure — e.g. a quarantined rule under fail-fast —
    /// throws synchronously out of this call, the same way an eager <c>MapMotivRules</c> resolution
    /// already does elsewhere in this suite.
    /// </remarks>
    /// <param name="configure">Enrolls rules and configures the builder, e.g. <c>AddRuleStore</c>.</param>
    public static TestHost Create(Action<MotivRulesBuilder>? configure = null)
    {
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var options = new MotivRulesOptions().AddModel<Customer>("customer");

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestAuth();

        var motiv = builder.Services.AddMotivRules(registry, options).AddRule<SampleRule>();
        configure?.Invoke(motiv);

        var app = builder.Build();
        app.UseTestAuth();
        app.MapMotivRules("/api/rules"); // resolves the RuleSet eagerly — may throw on a quarantined load
        app.Start();

        return new TestHost(app);
    }

    /// <summary>
    /// A started test host from <see cref="Create"/>, wrapping its <see cref="HttpClient"/>. Named
    /// <c>TestHost</c> rather than <c>Host</c> so it does not shadow
    /// <see cref="Microsoft.Extensions.Hosting.Host"/>, which this file's <c>using</c> for
    /// <see cref="HostingAbstractionsHostExtensions.Start"/> brings into scope.
    /// </summary>
    internal sealed class TestHost(WebApplication app) : IAsyncDisposable
    {
        public HttpClient Client { get; } = app.GetTestClient();

        /// <summary>The host's service provider, for tests that assert on what was registered.</summary>
        public IServiceProvider Services => app.Services;

        public ValueTask DisposeAsync() => app.DisposeAsync();
    }
}
