using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>
/// A host whose audited rule has logged one decision, with the sink registered as the source: what
/// the decision endpoints and the MCP tools are both exercised against, so the two are compared
/// over the same log. Grants start at full rights and can be narrowed after start-up.
/// </summary>
internal sealed class DecisionHost : IAsyncDisposable
{
    /// <summary>The model the host's rule decides over.</summary>
    public sealed record Customer(bool IsActive, int Age, string? Id = null);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class ActiveRule() : Rule<Customer, string>("active-rule", IsActive);

    /// <summary>A rule whose version 1 is a document, not compiled code, as Studio's loyalty-discount is.</summary>
    private sealed class DocumentRule() : Rule<Customer, string>(
        "document-rule", RuleDocuments.FromJson("""{ "rule": { "spec": "is-active" } }"""));

    public required WebApplication App { get; init; }

    public required HttpClient Http { get; init; }

    public required McpClient Mcp { get; init; }

    public required Guid DecisionId { get; init; }

    public required SwappableGrants Grants { get; init; }

    /// <param name="log">How the log captures the model; a whole capture of the customer by default.</param>
    public static async Task<DecisionHost> StartAsync(Action<DecisionLogOptions>? log = null)
    {
        var sink = new InMemoryDecisionSink();
        var grants = new SwappableGrants();
        var registry = new SpecRegistry().Register("is-active", IsActive);
        var options = new MotivRulesOptions().AddModel<Customer>("customer");
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestAuth();
        builder.Services.AddSingleton<IGrantSource>(grants);
        builder.Services.AddMotivRules(registry, options)
            .AddRule<ActiveRule>()
            .AddRule<DocumentRule>()
            .AddRuleStore()
            .AddPropositions()
            .AddScenarios()
            .AddDecisionLog(sink, decisionLog =>
            {
                decisionLog.Backpressure = DecisionBackpressure.Block;
                if (log is null)
                    decisionLog.Capture.StoreWhole<Customer>();
                else
                    log(decisionLog);
            })
            .AddDecisionSource(sink)
            .AddMcp();
        var app = builder.Build();
        app.UseTestAuth();
        app.MapMotivRules("/api/rules");
        app.MapMotivMcp("/mcp");
        await app.StartAsync();

        var http = app.GetTestClient();
        var document = JsonDocument.Parse("""{ "audited": true, "rule": { "spec": "is-active" } }""").RootElement;
        (await http.PutAsJsonAsync("/api/rules/rules/active-rule", new { document, baseVersion = 1 })).EnsureSuccessStatusCode();
        (await http.PutAsJsonAsync("/api/rules/rules/document-rule", new { document, baseVersion = 1 })).EnsureSuccessStatusCode();
        (await http.PostAsJsonAsync("/api/rules/rules/active-rule/evaluate", new { model = new { isActive = true, age = 30, id = "cust-42" } })).EnsureSuccessStatusCode();

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (sink.Records.Count == 0 && DateTime.UtcNow < deadline)
            await Task.Delay(10);

        var transport = new HttpClientTransport(new HttpClientTransportOptions { Endpoint = new Uri(http.BaseAddress!, "/mcp") }, http, null, false);
        var mcp = await McpClient.CreateAsync(transport);
        return new DecisionHost { App = app, Http = http, Mcp = mcp, DecisionId = sink.Records[0].Id, Grants = grants };
    }

    /// <summary>Calls one tool, with <paramref name="args"/> serialised as the tool's arguments.</summary>
    public async Task<CallToolResult> CallAsync(string tool, object? args = null)
    {
        var arguments = args is null ? null : JsonSerializer.Deserialize<Dictionary<string, object?>>(JsonSerializer.Serialize(args));
        return await Mcp.CallToolAsync(tool, arguments);
    }

    public async ValueTask DisposeAsync()
    {
        await Mcp.DisposeAsync();
        await App.DisposeAsync();
    }
}
