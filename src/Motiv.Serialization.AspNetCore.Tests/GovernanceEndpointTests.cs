using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>
/// The change-request surface end to end, plus the no-bypass rewiring: with governance registered a
/// direct write publishes through the same gate, and with the permissive default its response stays
/// byte-for-byte what it was before governance existed.
/// </summary>
public class GovernanceEndpointTests
{
    private const string MakerCheckerGate =
        """
        {"rule": {"and": [
            {"spec": "change.approver-count-at-least", "args": {"n": 1}},
            {"not": {"spec": "change.author-is-approver"}}
        ]}}
        """;

    private const string RuleName = "checkout.can-checkout";

    private sealed record Customer(bool IsActive, int Age);

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static SpecBase<Customer, string> IsAdult { get; } =
        Spec.Build((Customer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create();

    private sealed class CanCheckoutRule() : Rule<Customer, string>(RuleName, IsActive);

    /// <summary>The one document every test swaps the rule to — it binds, and it is not the default.</summary>
    private static JsonElement AdultDocument =>
        JsonDocument.Parse("""{ "rule": { "spec": "customer.is-adult" } }""").RootElement;

    private static async Task<WebApplication> StartAsync(bool governed, params NamespaceGrant[] grants)
    {
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.is-adult", IsAdult);
        var options = new MotivRulesOptions().AddModel<Customer>("customer");

        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddTestAuth();
        if (grants.Length > 0)
            builder.Services.AddSingleton<IGrantSource>(
                new GrantEnforcementTests.FakeGrantSource(grants));

        var motiv = builder.Services.AddMotivRules(registry, options)
            .AddPropositions()
            .AddRule<CanCheckoutRule>();
        if (governed)
            motiv.AddGovernance();

        var app = builder.Build();
        app.UseTestAuth();
        app.MapMotivRules("/api/rules");
        await app.StartAsync();
        return app;
    }

    private static Task<HttpResponseMessage> Send(
        HttpClient client, HttpMethod method, string url, string? user = null, object? body = null)
    {
        var request = new HttpRequestMessage(method, url);
        if (user is not null)
            request.Headers.Add(TestAuthHandler.SubjectHeader, user);
        if (body is not null)
            request.Content = JsonContent.Create(body);
        return client.SendAsync(request);
    }

    private static object OneRuleChange(string name, JsonElement document, int baseVersion) => new
    {
        changeNote = "swap the eligibility check",
        changes = new[] { new { kind = "rule", name, document, baseVersion } }
    };

    private static async Task<Guid> IdOf(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid();

    private static async Task<int> RuleVersion(HttpClient client, string name) =>
        (await client.GetFromJsonAsync<JsonElement>($"/api/rules/rules/{name}"))
        .GetProperty("version").GetInt32();

    [Fact]
    public async Task Should_carry_a_change_request_from_creation_through_approval_to_publication()
    {
        // Arrange — one grant covering the whole checkout namespace, so the author may author and
        // the checker may publish.
        await using var app = await StartAsync(governed: true, new NamespaceGrant("checkout", GrantVerb.Publish));
        var client = app.GetTestClient();

        // Act — author, then approve as somebody else, then publish
        var created = await Send(client, HttpMethod.Post, "/api/rules/change-requests",
            "author", OneRuleChange(RuleName, AdultDocument, 1));
        var id = await IdOf(created);

        var approved = await Send(client, HttpMethod.Post, $"/api/rules/change-requests/{id}/approvals", "checker");
        var published = await Send(client, HttpMethod.Post, $"/api/rules/change-requests/{id}/publish", "checker");

        // Assert — the workflow ran and the live rule moved
        created.StatusCode.ShouldBe(HttpStatusCode.Created);
        approved.StatusCode.ShouldBe(HttpStatusCode.OK);
        published.StatusCode.ShouldBe(HttpStatusCode.OK);

        var body = await published.Content.ReadFromJsonAsync<JsonElement>();
        body.GetProperty("request").GetProperty("status").GetString()!.ShouldBe("Published");
        body.GetProperty("publishedVersions").GetProperty(RuleName).GetInt32().ShouldBe(2);
        (await RuleVersion(client, RuleName)).ShouldBe(2);
    }

    [Fact]
    public async Task Should_refuse_a_change_request_touching_one_target_the_author_may_not_author()
    {
        // Arrange — author on 'checkout' only
        await using var app = await StartAsync(governed: true, new NamespaceGrant("checkout", GrantVerb.Author));
        var client = app.GetTestClient();

        // Act — an envelope straddling the grant boundary
        var response = await Send(client, HttpMethod.Post, "/api/rules/change-requests", "author", new
        {
            changeNote = "two targets",
            changes = new[]
            {
                new { kind = "rule", name = RuleName, document = AdultDocument, baseVersion = 1 },
                new { kind = "rule", name = "pricing.discount", document = AdultDocument, baseVersion = 1 }
            }
        });

        // Assert — refused whole, naming the verb it wanted
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).ShouldContain("author");
    }

    [Fact]
    public async Task Should_answer_404_for_an_unknown_change_request()
    {
        // Arrange
        await using var app = await StartAsync(governed: true);
        var client = app.GetTestClient();

        // Act
        var get = await client.GetAsync($"/api/rules/change-requests/{Guid.NewGuid()}");
        var publish = await Send(client, HttpMethod.Post, $"/api/rules/change-requests/{Guid.NewGuid()}/publish");

        // Assert
        get.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        publish.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Should_refuse_a_withdrawal_by_anybody_but_the_author()
    {
        // Arrange
        await using var app = await StartAsync(governed: true);
        var client = app.GetTestClient();
        var id = await IdOf(await Send(client, HttpMethod.Post, "/api/rules/change-requests",
            "author", OneRuleChange(RuleName, AdultDocument, 1)));

        // Act
        var interloper = await Send(client, HttpMethod.Post, $"/api/rules/change-requests/{id}/withdrawal", "someone-else");
        var author = await Send(client, HttpMethod.Post, $"/api/rules/change-requests/{id}/withdrawal", "author");

        // Assert
        interloper.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        author.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await author.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("status").GetString()!.ShouldBe("Withdrawn");
    }

    [Fact]
    public async Task Should_refuse_a_direct_rule_write_that_the_gate_would_not_let_through()
    {
        // Arrange — maker-checker: one approval, and not the author's own
        await using var app = await StartAsync(governed: true);
        var client = app.GetTestClient();
        app.Services.GetRequiredService<ApprovalGate>()
            .SetGate(MakerCheckerGate, []).Outcome.ShouldBe(GateUpdateOutcome.Updated);

        // Act — the direct write is a change request with no approvals on it
        var direct = await client.PutAsJsonAsync($"/api/rules/rules/{RuleName}",
            new { document = AdultDocument, baseVersion = 1 });

        // Assert — refused, in the gate's own words, and the rule never moved
        direct.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        var refusal = await direct.Content.ReadFromJsonAsync<JsonElement>();
        refusal.GetProperty("reason").GetString()!.ShouldContain("approvals");
        refusal.GetProperty("justification").GetString()!.ShouldContain("fewer than 1 approvals");
        refusal.GetProperty("assertions").GetArrayLength().ShouldBeGreaterThan(0);
        (await RuleVersion(client, RuleName)).ShouldBe(1);
    }

    [Fact]
    public async Task Should_let_the_same_edit_through_once_a_peer_has_approved_it()
    {
        // Arrange — the same gate that refused the direct write above
        await using var app = await StartAsync(governed: true);
        var client = app.GetTestClient();
        app.Services.GetRequiredService<ApprovalGate>().SetGate(MakerCheckerGate, []);

        // Act — the ceremony the gate is asking for
        var id = await IdOf(await Send(client, HttpMethod.Post, "/api/rules/change-requests",
            "author", OneRuleChange(RuleName, AdultDocument, 1)));
        var selfPublish = await Send(client, HttpMethod.Post, $"/api/rules/change-requests/{id}/publish", "author");
        await Send(client, HttpMethod.Post, $"/api/rules/change-requests/{id}/approvals", "checker");
        var peerPublish = await Send(client, HttpMethod.Post, $"/api/rules/change-requests/{id}/publish", "author");

        // Assert — blocked while unapproved, through once approved
        selfPublish.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        peerPublish.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RuleVersion(client, RuleName)).ShouldBe(2);
    }

    [Fact]
    public async Task Should_leave_the_direct_rule_write_response_identical_under_the_permissive_default()
    {
        // Arrange — the same host twice, once with governance and once without
        await using var governed = await StartAsync(governed: true);
        await using var legacy = await StartAsync(governed: false);

        // Act
        var underGovernance = await governed.GetTestClient()
            .PutAsJsonAsync($"/api/rules/rules/{RuleName}", new { document = AdultDocument, baseVersion = 1 });
        var withoutGovernance = await legacy.GetTestClient()
            .PutAsJsonAsync($"/api/rules/rules/{RuleName}", new { document = AdultDocument, baseVersion = 1 });

        // Assert — byte-for-byte the same answer, and the rule really was published
        underGovernance.StatusCode.ShouldBe(withoutGovernance.StatusCode);
        (await underGovernance.Content.ReadAsStringAsync())
            .ShouldBe(await withoutGovernance.Content.ReadAsStringAsync());
        (await RuleVersion(governed.GetTestClient(), RuleName)).ShouldBe(2);
    }

    [Fact]
    public async Task Should_leave_a_refused_direct_write_answering_exactly_as_it_did_before()
    {
        // A governed publish refuses in ChangeRequest terms, which are not the terms the rule
        // surface has always answered in — so a refusal falls through to the ungoverned write,
        // whose refusal is the one callers (and the demo UI) already parse.

        // Arrange
        await using var governed = await StartAsync(governed: true);
        await using var legacy = await StartAsync(governed: false);
        var stale = new { document = AdultDocument, baseVersion = 7 };
        var unknown = $"/api/rules/rules/no-such-rule";

        // Act
        var governedConflict = await governed.GetTestClient().PutAsJsonAsync($"/api/rules/rules/{RuleName}", stale);
        var legacyConflict = await legacy.GetTestClient().PutAsJsonAsync($"/api/rules/rules/{RuleName}", stale);
        var governedUnknown = await governed.GetTestClient().PutAsJsonAsync(unknown, new { document = AdultDocument, baseVersion = 1 });
        var legacyUnknown = await legacy.GetTestClient().PutAsJsonAsync(unknown, new { document = AdultDocument, baseVersion = 1 });

        // Assert
        governedConflict.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        (await governedConflict.Content.ReadAsStringAsync())
            .ShouldBe(await legacyConflict.Content.ReadAsStringAsync());
        governedUnknown.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await governedUnknown.Content.ReadAsStringAsync())
            .ShouldBe(await legacyUnknown.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Should_publish_a_proposition_write_through_the_gate_without_changing_its_response()
    {
        // Arrange
        await using var governed = await StartAsync(governed: true);
        await using var legacy = await StartAsync(governed: false);
        var create = new
        {
            name = "customer.is-eligible",
            modelType = "customer",
            document = JsonDocument.Parse("""{ "rule": { "spec": "customer.is-adult" } }""").RootElement,
            description = (string?)null
        };

        // Act
        var underGovernance = await governed.GetTestClient().PostAsJsonAsync("/api/rules/propositions", create);
        var withoutGovernance = await legacy.GetTestClient().PostAsJsonAsync("/api/rules/propositions", create);

        // Assert — same status, same body, and the proposition is really live
        underGovernance.StatusCode.ShouldBe(HttpStatusCode.Created);
        underGovernance.StatusCode.ShouldBe(withoutGovernance.StatusCode);
        (await underGovernance.Content.ReadAsStringAsync())
            .ShouldBe(await withoutGovernance.Content.ReadAsStringAsync());
        governed.Services.GetRequiredService<PropositionSet>()
            .DocumentJsonOf("customer.is-eligible").ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_still_refuse_a_direct_write_the_caller_has_no_grant_for_before_the_gate_sees_it()
    {
        // Arrange — no publish grant on checkout at all, and a gate that would refuse anyway
        await using var app = await StartAsync(governed: true, new NamespaceGrant("pricing", GrantVerb.Publish));
        var client = app.GetTestClient();
        app.Services.GetRequiredService<ApprovalGate>().SetGate(MakerCheckerGate, []);

        // Act
        var response = await client.PutAsJsonAsync($"/api/rules/rules/{RuleName}",
            new { document = AdultDocument, baseVersion = 1 });

        // Assert — the grant refusal, not the gate refusal: grants run first, as they always did
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        (await response.Content.ReadAsStringAsync()).ShouldContain("publish");
        app.Services.GetRequiredService<ChangeRequestSet>().All.ShouldBeEmpty();
    }
}
