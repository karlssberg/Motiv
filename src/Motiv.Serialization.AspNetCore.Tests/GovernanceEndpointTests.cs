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
        // Once the gate allows the write, the core that runs is the very one the ungoverned
        // endpoint calls — so every refusal below the gate comes back in the words callers (and the
        // demo UI) already parse, with no second execution and no mapping to invent.

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
            // Non-null on purpose: a governed create routes through a different call path, and a
            // dropped description would be invisible to a null-description test.
            description = "eligible for the loyalty tier"
        };

        // Act
        var underGovernance = await governed.GetTestClient().PostAsJsonAsync("/api/rules/propositions", create);
        var withoutGovernance = await legacy.GetTestClient().PostAsJsonAsync("/api/rules/propositions", create);

        // Assert — same status, same body, and the two stores hold the same proposition, description included
        underGovernance.StatusCode.ShouldBe(HttpStatusCode.Created);
        underGovernance.StatusCode.ShouldBe(withoutGovernance.StatusCode);
        (await underGovernance.Content.ReadAsStringAsync())
            .ShouldBe(await withoutGovernance.Content.ReadAsStringAsync());

        (await governed.GetTestClient().GetStringAsync("/api/rules/propositions"))
            .ShouldBe(await legacy.GetTestClient().GetStringAsync("/api/rules/propositions"));
        governed.Services.GetRequiredService<PropositionSet>()
            .Find("customer.is-eligible")!.Description!.ShouldBe("eligible for the loyalty tier");
    }

    [Fact]
    public async Task Should_answer_a_referenced_proposition_deletion_exactly_as_the_ungoverned_surface_does()
    {
        // The refusal a change request cannot restate: PropositionReferencedResponse carries the
        // referrer list the demo UI renders. Running the core itself is what keeps it.

        // Arrange — a proposition another proposition is derived from, on both hosts
        await using var governed = await StartAsync(governed: true);
        await using var legacy = await StartAsync(governed: false);

        async Task Seed(WebApplication app)
        {
            var client = app.GetTestClient();
            await client.PostAsJsonAsync("/api/rules/propositions", new
            {
                name = "customer.base", modelType = "customer",
                document = JsonDocument.Parse("""{ "rule": { "spec": "customer.is-active" } }""").RootElement,
                description = (string?)null
            });
            await client.PostAsJsonAsync("/api/rules/propositions", new
            {
                name = "customer.derived", modelType = "customer",
                document = JsonDocument.Parse("""{ "rule": { "spec": "customer.base" } }""").RootElement,
                description = (string?)null
            });
        }

        await Seed(governed);
        await Seed(legacy);

        // Act
        var underGovernance = await governed.GetTestClient()
            .DeleteAsync("/api/rules/propositions/customer.base?baseVersion=1");
        var withoutGovernance = await legacy.GetTestClient()
            .DeleteAsync("/api/rules/propositions/customer.base?baseVersion=1");

        // Assert — the same 409, naming the same referrer
        underGovernance.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        underGovernance.StatusCode.ShouldBe(withoutGovernance.StatusCode);
        var body = await underGovernance.Content.ReadAsStringAsync();
        body.ShouldBe(await withoutGovernance.Content.ReadAsStringAsync());
        body.ShouldContain("customer.derived");
    }

    [Fact]
    public async Task Should_mint_no_change_request_for_a_direct_write_whether_it_lands_or_is_refused()
    {
        // A direct write is not a proposal. Recording one would leave GET /change-requests full of
        // rows nobody raised, and a refused write wedged in Draft looking like a live proposal.

        // Arrange
        await using var app = await StartAsync(governed: true);
        var client = app.GetTestClient();
        var changes = app.Services.GetRequiredService<ChangeRequestSet>();

        // Act — one that lands, then a gate that refuses, then one that is refused
        var landed = await client.PutAsJsonAsync($"/api/rules/rules/{RuleName}",
            new { document = AdultDocument, baseVersion = 1 });
        app.Services.GetRequiredService<ApprovalGate>().SetGate(MakerCheckerGate, []);
        var refused = await client.PutAsJsonAsync($"/api/rules/rules/{RuleName}",
            new { document = AdultDocument, baseVersion = 2 });

        // Assert
        landed.StatusCode.ShouldBe(HttpStatusCode.OK);
        refused.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        changes.All.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_count_one_approver_once_however_often_they_approve()
    {
        // Arrange — two pairs of eyes required
        await using var app = await StartAsync(governed: true);
        var client = app.GetTestClient();
        app.Services.GetRequiredService<ApprovalGate>().SetGate(
            """{"rule": {"spec": "change.approver-count-at-least", "args": {"n": 2}}}""", []);
        var id = await IdOf(await Send(client, HttpMethod.Post, "/api/rules/change-requests",
            "author", OneRuleChange(RuleName, AdultDocument, 1)));

        // Act — the same checker twice, then a second, distinct one
        await Send(client, HttpMethod.Post, $"/api/rules/change-requests/{id}/approvals", "checker");
        await Send(client, HttpMethod.Post, $"/api/rules/change-requests/{id}/approvals", "checker");
        var afterRepeat = await Send(client, HttpMethod.Post, $"/api/rules/change-requests/{id}/publish", "author");

        await Send(client, HttpMethod.Post, $"/api/rules/change-requests/{id}/approvals", "second-checker");
        var afterSecond = await Send(client, HttpMethod.Post, $"/api/rules/change-requests/{id}/publish", "author");

        // Assert — pressing the button twice is one approver, not two
        afterRepeat.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        afterSecond.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetFromJsonAsync<JsonElement>($"/api/rules/change-requests/{id}"))
            .GetProperty("approvals").GetArrayLength().ShouldBe(2);
    }

    [Fact]
    public async Task Should_refuse_a_proposed_change_whose_document_is_absent_rather_than_read_it_as_a_deletion()
    {
        // Arrange
        await using var app = await StartAsync(governed: true);
        var client = app.GetTestClient();

        // Act — no document property at all, then an explicit null
        var absent = await Send(client, HttpMethod.Post, "/api/rules/change-requests", "author", new
        {
            changeNote = "oops",
            changes = new[] { new { kind = "rule", name = RuleName, baseVersion = 1 } }
        });
        var explicitNull = await Send(client, HttpMethod.Post, "/api/rules/change-requests", "author", new
        {
            changeNote = "revert to the default",
            changes = new[]
            {
                new { kind = "rule", name = RuleName, document = (JsonElement?)null, baseVersion = 1 }
            }
        });

        // Assert — omission is a mistake; null is a deletion
        absent.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await absent.Content.ReadAsStringAsync()).ShouldContain(RuleName);
        explicitNull.StatusCode.ShouldBe(HttpStatusCode.Created);
        (await explicitNull.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("changes")[0].GetProperty("classification")
            .GetProperty("isDeletion").GetBoolean().ShouldBeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Should_refuse_a_proposed_change_with_a_blank_name_rather_than_500(string? name)
    {
        // Arrange
        await using var app = await StartAsync(governed: true);
        var client = app.GetTestClient();

        // Act
        var response = await Send(client, HttpMethod.Post, "/api/rules/change-requests", "author", new
        {
            changeNote = "oops",
            changes = new[] { new { kind = "rule", name, document = AdultDocument, baseVersion = 1 } }
        });

        // Assert — a 400 naming the offending entry, not a 500 from a null flowing into a lookup
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).ShouldContain("Change 0");
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
