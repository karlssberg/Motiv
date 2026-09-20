using System.Net;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>
/// The MCP endpoint is secured as the rules API is: authorization by default, and the
/// <c>anonymous</c> flag at the mount site is a real <c>AllowAnonymous</c>, so it holds even under
/// a host whose fallback policy requires an authenticated user.
/// </summary>
public class McpAuthorizationTests
{
    [Fact]
    public async Task Should_reject_an_unauthenticated_caller_with_401_by_default()
    {
        await using var app = await StartAsync(anonymous: false, fallbackPolicy: false);
        var client = AnonymousClient(app);

        var response = await client.PostAsync("/mcp", new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Should_serve_an_anonymous_caller_under_a_fallback_policy_when_the_mount_site_opts_out()
    {
        await using var app = await StartAsync(anonymous: true, fallbackPolicy: true);
        var client = AnonymousClient(app);

        var transport = new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(client.BaseAddress!, "/mcp") }, client, null, false);
        await using var mcp = await McpClient.CreateAsync(transport);

        (await mcp.ListToolsAsync()).ShouldNotBeEmpty();
    }

    [Fact]
    public void Should_refuse_a_second_AddMcp()
    {
        var rules = new ServiceCollection().AddMotivRules(new SpecRegistry(), new MotivRulesOptions()).AddMcp();

        var refused = Should.Throw<InvalidOperationException>(() => rules.AddMcp());

        refused.Message.ShouldContain("AddMcp has already been called");
    }

    private static HttpClient AnonymousClient(WebApplication app)
    {
        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(TestAuthHandler.AnonymousHeader, "true");
        return client;
    }

    private static async Task<WebApplication> StartAsync(bool anonymous, bool fallbackPolicy)
    {
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestAuth();
        if (fallbackPolicy)
            builder.Services.AddAuthorization(o => o.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());
        builder.Services.AddMotivRules(new SpecRegistry(), new MotivRulesOptions()).AddMcp();
        var app = builder.Build();
        app.UseTestAuth();
        app.MapMotivMcp("/mcp", anonymous);
        await app.StartAsync();
        return app;
    }
}
