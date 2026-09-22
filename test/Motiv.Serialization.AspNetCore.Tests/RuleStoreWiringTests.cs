using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>
/// The wiring that makes durability reachable from a host: <see cref="MotivRulesBuilder.AddRuleStore"/>
/// registering a store and loading it at startup, and the change-note/author plumbing on the write
/// endpoints. Uses <see cref="TestApp.Create"/>, which enrolls one spec (<c>customer.is-active</c>)
/// and one rule (<c>sample</c>) via DI, so <c>AddRuleStore</c> is reachable on the builder.
/// </summary>
public class RuleStoreWiringTests
{
    private static JsonElement DocumentReferencing(string spec) =>
        JsonDocument.Parse($$"""{ "rule": { "spec": "{{spec}}" } }""").RootElement;

    /// <summary>
    /// A store holding one stored head for <c>sample</c> that no longer binds — the shape a redeploy
    /// leaves behind when it renames the spec a published document referenced.
    /// </summary>
    private static async Task<InMemoryRuleStore> StoreWithAnUnbindableHead()
    {
        var store = new InMemoryRuleStore();
        await store.AppendAsync([new StoredRuleVersion(
            "sample", 2, """{ "rule": { "spec": "customer.was-renamed-away" } }""",
            "alice", DateTimeOffset.UnixEpoch, null, null, "test")], default);

        return store;
    }

    [Fact]
    public async Task Should_survive_a_restart_when_a_rule_store_is_registered()
    {
        // Arrange — one store, two app lifetimes
        var store = new InMemoryRuleStore();
        var document = DocumentReferencing("customer.is-active");

        await using (var first = TestApp.Create(builder => builder.AddRuleStore(store)))
        {
            var response = await first.Client.PutAsJsonAsync(
                "/api/rules/rules/sample", new { document, baseVersion = 1 });
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        // Act — a fresh host, same store: the second lifetime must pick up what the first published
        await using var second = TestApp.Create(builder => builder.AddRuleStore(store));
        var reloaded = await second.Client.GetFromJsonAsync<JsonElement>("/api/rules/rules/sample");

        // Assert
        reloaded.GetProperty("version").GetInt32().ShouldBe(2);
        reloaded.GetProperty("document").GetProperty("rule").GetProperty("spec")
            .GetString()!.ShouldBe("customer.is-active");
    }

    [Fact]
    public async Task Should_refuse_startup_when_a_stored_rule_no_longer_binds()
    {
        // Arrange — a redeploy renamed the spec the stored document referenced
        var store = await StoreWithAnUnbindableHead();

        // Act / Assert — fail-fast is the default: a silent revert to unapproved behaviour is worse
        var exception = Should.Throw<RuleSerializationException>(() =>
            TestApp.Create(builder => builder.AddRuleStore(store)));
        exception.Message.ShouldContain("quarantined");
    }

    [Fact]
    public async Task Should_boot_with_the_quarantine_reported_when_fail_fast_is_off()
    {
        // Arrange
        var store = await StoreWithAnUnbindableHead();

        // Act
        await using var app = TestApp.Create(
            builder => builder.AddRuleStore(store, failFastOnQuarantine: false));
        var listed = await app.Client.GetFromJsonAsync<JsonElement>("/api/rules/rules/sample");

        // Assert — booted, but the catalog says so rather than pretending the default is what was published
        listed.GetProperty("quarantine").GetArrayLength().ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task Should_record_the_authenticated_principal_as_the_author()
    {
        // Arrange — spec 1 made every authoring endpoint authenticated, so there is always a principal
        var store = new InMemoryRuleStore();
        await using var app = TestApp.Create(builder => builder.AddRuleStore(store));

        // Act
        await app.Client.PutAsJsonAsync("/api/rules/rules/sample", new
        {
            document = DocumentReferencing("customer.is-active"),
            baseVersion = 1,
            changeNote = "via the endpoint"
        });

        // Assert
        var history = await store.HistoryAsync("sample", default);
        history.ShouldHaveSingleItem();
        history[0].Author.ShouldNotBe("unknown");
        history[0].ChangeNote!.ShouldBe("via the endpoint");
    }

    [Fact]
    public async Task Should_record_the_callers_change_note_under_governance_too()
    {
        // Arrange — governance is permissive by default (no gate document installed), so a governed
        // direct write publishes exactly like the ungoverned one, but through DirectWriteAsync, which
        // used to synthesise its own note and silently drop whatever the caller sent.
        var store = new InMemoryRuleStore();
        await using var app = TestApp.Create(builder => builder
            .AddRuleStore(store)
            .AddGovernance());

        // Act
        var response = await app.Client.PutAsJsonAsync("/api/rules/rules/sample", new
        {
            document = DocumentReferencing("customer.is-active"),
            baseVersion = 1,
            changeNote = "via the governed endpoint"
        });

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var history = await store.HistoryAsync("sample", default);
        history.ShouldHaveSingleItem();
        history[0].ChangeNote!.ShouldBe("via the governed endpoint");
    }

    [Fact]
    public async Task Should_load_a_stored_rule_referencing_an_authored_proposition_without_quarantine()
    {
        // Arrange — an authored proposition, and a stored rule document that references it. Only a
        // clean load proves the ordering the DI factory promises: PropositionSet.Load() before every
        // rule is Add-ed, and RuleSet.Load() only after that. Get either wrong and "customer.eligible"
        // is unresolvable when the stored document is bound, and the rule quarantines despite being
        // perfectly healthy.
        var propositions = new InMemoryPropositionStore();
        await propositions.WriteAsync(
            PropositionBatch.Save(new StoredProposition(
                "customer.eligible", "customer",
                """{ "rule": { "spec": "customer.is-active" } }""", 1, null)),
            default);

        var rules = new InMemoryRuleStore();
        await rules.AppendAsync([new StoredRuleVersion(
            "sample", 2, """{ "rule": { "spec": "customer.eligible" } }""",
            "alice", DateTimeOffset.UnixEpoch, null, null, "test")], default);

        // Act
        await using var app = TestApp.Create(builder => builder
            .AddPropositions(propositions)
            .AddRuleStore(rules));
        var listed = await app.Client.GetFromJsonAsync<JsonElement>("/api/rules/rules/sample");

        // Assert — a clean load: no quarantine, and the rule resolved the stored document, spec and
        // all, rather than falling back to its compiled default
        listed.GetProperty("quarantine").GetArrayLength().ShouldBe(0);
        listed.GetProperty("version").GetInt32().ShouldBe(2);
        listed.GetProperty("document").GetProperty("rule").GetProperty("spec")
            .GetString()!.ShouldBe("customer.eligible");
    }

    [Fact]
    public void Should_reject_a_second_AddRuleStore_call()
    {
        // Arrange — DI is last-wins, so a second call would silently discard the first store: no
        // double Load, no exception, an argument quietly ignored. AddPropositions/AddGovernance
        // already guard the same way; this is the same contract for the third builder method.
        var registry = new SpecRegistry();
        var options = new MotivRulesOptions();
        var builder = new ServiceCollection().AddMotivRules(registry, options);
        builder.AddRuleStore();

        // Act
        var second = () => builder.AddRuleStore(new InMemoryRuleStore());

        // Assert
        second.ShouldThrow<InvalidOperationException>();
    }
}
