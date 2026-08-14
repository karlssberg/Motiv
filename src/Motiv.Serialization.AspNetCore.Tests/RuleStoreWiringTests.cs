using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

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
        var store = new InMemoryRuleStore();
        await store.AppendAsync([new StoredRuleVersion(
            "sample", 2, """{ "rule": { "spec": "customer.was-renamed-away" } }""",
            "alice", DateTimeOffset.UnixEpoch, null, null, "test")], default);

        // Act / Assert — fail-fast is the default: a silent revert to unapproved behaviour is worse
        var exception = Should.Throw<RuleSerializationException>(() =>
            TestApp.Create(builder => builder.AddRuleStore(store)));
        exception.Message.ShouldContain("quarantined");
    }

    [Fact]
    public async Task Should_boot_with_the_quarantine_reported_when_fail_fast_is_off()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        await store.AppendAsync([new StoredRuleVersion(
            "sample", 2, """{ "rule": { "spec": "customer.was-renamed-away" } }""",
            "alice", DateTimeOffset.UnixEpoch, null, null, "test")], default);

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
}
