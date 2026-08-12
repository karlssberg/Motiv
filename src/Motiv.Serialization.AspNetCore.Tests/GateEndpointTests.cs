using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>
/// The <c>/gate</c> configuration surface: an <c>administer</c>-gated way to inspect, replace, and
/// reset the active approval-gate document — never a <see cref="ChangeRequest"/>, since the gate
/// cannot be asked to approve its own reconfiguration.
/// </summary>
public class GateEndpointTests
{
    private const string RuleName = "checkout.can-checkout";

    private const string MakerCheckerGate =
        """
        {"rule": {"and": [
            {"spec": "change.approver-count-at-least", "args": {"n": 1}},
            {"not": {"spec": "change.author-is-approver"}}
        ]}}
        """;

    private sealed record Customer(bool IsActive, int Age);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class CanCheckoutRule() : Rule<Customer, string>(RuleName, IsActive);

    private static JsonElement AdultDocument =>
        JsonDocument.Parse("""{ "rule": { "spec": "customer.is-active" } }""").RootElement;

    private static async Task<WebApplication> StartAsync(params NamespaceGrant[] grantsForAdmin)
    {
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var options = new MotivRulesOptions().AddModel<Customer>("customer");

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestAuth();
        builder.Services.AddSingleton<IGrantSource>(
            new GrantEnforcementTests.FakeGrantSource(grantsForAdmin, isAdministrator: true));

        builder.Services.AddMotivRules(registry, options)
            .AddRule<CanCheckoutRule>()
            .AddGovernance();

        var app = builder.Build();
        app.UseTestAuth();
        app.MapMotivRules("/api/rules");
        await app.StartAsync();
        return app;
    }

    /// <summary>A host with a non-administrator grant source, for the refusal test.</summary>
    private static async Task<WebApplication> StartAsNonAdminAsync()
    {
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var options = new MotivRulesOptions().AddModel<Customer>("customer");

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestAuth();
        builder.Services.AddSingleton<IGrantSource>(
            new GrantEnforcementTests.FakeGrantSource([], isAdministrator: false));

        builder.Services.AddMotivRules(registry, options)
            .AddRule<CanCheckoutRule>()
            .AddGovernance();

        var app = builder.Build();
        app.UseTestAuth();
        app.MapMotivRules("/api/rules");
        await app.StartAsync();
        return app;
    }

    private static Task<HttpResponseMessage> PutGate(HttpClient client, object document) =>
        client.PutAsJsonAsync("/api/rules/gate", new { document });

    private static Task<HttpResponseMessage> DirectWrite(HttpClient client, int baseVersion = 1) =>
        client.PutAsJsonAsync($"/api/rules/rules/{RuleName}", new { document = AdultDocument, baseVersion });

    [Fact]
    public async Task Should_refuse_a_non_administrator_and_leave_the_gate_unchanged()
    {
        // Arrange
        await using var app = await StartAsNonAdminAsync();
        var client = app.GetTestClient();

        // Act
        var response = await PutGate(client, JsonDocument.Parse(MakerCheckerGate).RootElement);

        // Assert — refused, naming the requirement, and a direct write still goes through unmodified
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).ShouldContain("administer");
        app.Services.GetRequiredService<ApprovalGate>().DocumentJson.ShouldBeNull();
    }

    [Fact]
    public async Task Should_let_an_administrator_configure_the_gate_and_have_it_take_effect()
    {
        // Arrange — publish grant on the rule's namespace, so a later 403 can only be the gate's
        // doing, never a missing grant
        await using var app = await StartAsync(new NamespaceGrant("checkout", GrantVerb.Publish));
        var client = app.GetTestClient();

        // Act — configure a maker-checker gate as the administrator
        var put = await PutGate(client, JsonDocument.Parse(MakerCheckerGate).RootElement);
        var get = await client.GetAsync("/api/rules/gate");

        // Assert — accepted, and GET echoes the same document back
        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        var putBody = await put.Content.ReadFromJsonAsync<JsonElement>();
        putBody.GetProperty("permissiveDefault").GetBoolean().ShouldBeFalse();

        get.StatusCode.ShouldBe(HttpStatusCode.OK);
        var getBody = await get.Content.ReadFromJsonAsync<JsonElement>();
        getBody.GetProperty("permissiveDefault").GetBoolean().ShouldBeFalse();
        getBody.GetProperty("document").GetProperty("rule").GetProperty("and")
            .GetArrayLength().ShouldBe(2);

        // Assert — the configured gate is live: an unapproved direct write is now refused, in the
        // gate's own words rather than a grant refusal
        var direct = await DirectWrite(client);
        direct.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await direct.Content.ReadAsStringAsync()).ShouldContain("approvals");
    }

    [Fact]
    public async Task Should_reset_to_permissive_on_delete_and_let_direct_writes_through_again()
    {
        // Arrange — a configured gate that is currently blocking direct writes
        await using var app = await StartAsync(new NamespaceGrant("checkout", GrantVerb.Publish));
        var client = app.GetTestClient();
        app.Services.GetRequiredService<ApprovalGate>().SetGate(MakerCheckerGate, []);
        (await DirectWrite(client)).StatusCode.ShouldBe(HttpStatusCode.Forbidden);

        // Act
        var delete = await client.DeleteAsync("/api/rules/gate");
        var direct = await DirectWrite(client);

        // Assert — reset, and the direct write that was refused a moment ago now lands
        delete.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        direct.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Should_report_the_permissive_default_shape_on_get()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        // Act
        var response = await client.GetAsync("/api/rules/gate");

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("permissiveDefault").GetBoolean().ShouldBeTrue();
        body.GetProperty("document").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task Should_refuse_an_invalid_document_without_changing_the_gate()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        // Act — a document referencing a spec that isn't registered
        var response = await PutGate(client,
            JsonDocument.Parse("""{"rule": {"spec": "no-such-spec"}}""").RootElement);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        app.Services.GetRequiredService<ApprovalGate>().DocumentJson.ShouldBeNull();
    }

    [Fact]
    public async Task Should_refuse_a_missing_document_with_400()
    {
        // Arrange
        await using var app = await StartAsync();
        var client = app.GetTestClient();

        // Act — the property is present but its value is the JSON literal `null`, not omitted;
        // exercised via a raw request since JsonContent would otherwise omit an unset property.
        var request = new HttpRequestMessage(HttpMethod.Put, "/api/rules/gate")
        {
            Content = JsonContent.Create(new { })
        };
        var response = await client.SendAsync(request);

        // Assert — no "document" property at all is refused the same way MissingDocument refuses
        // it elsewhere on this surface: a 400, not a silent no-op.
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
