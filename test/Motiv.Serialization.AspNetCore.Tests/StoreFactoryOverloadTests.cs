using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>
/// The container-built overloads of <see cref="MotivRulesBuilder.AddRuleStore"/> and
/// <see cref="MotivRulesBuilder.AddPropositions"/> — for a store with dependencies of its own (an
/// <c>IDbContextFactory</c>, say) that cannot be constructed before the container exists. Uses
/// <see cref="TestApp.Create"/>, the same host helper <c>RuleStoreWiringTests</c> uses; its
/// <c>TestHost</c> exposes only <c>Client</c>, not the underlying <c>IServiceProvider</c>, so these
/// tests confirm the factory-built store was actually wired in by round-tripping through the store
/// instance itself (held directly by the test) rather than resolving it back out of DI.
/// </summary>
public class StoreFactoryOverloadTests
{
    private static JsonElement DocumentReferencing(string spec) =>
        JsonDocument.Parse($$"""{ "rule": { "spec": "{{spec}}" } }""").RootElement;

    [Fact]
    public async Task Should_resolve_a_rule_store_built_from_the_container()
    {
        // Arrange — an EF store needs IDbContextFactory from DI, so it cannot be built before
        // the container exists; the factory overload defers construction until the provider is.
        var marker = new InMemoryRuleStore();

        // Act
        await using var app = TestApp.Create(builder => builder.AddRuleStore(provider =>
        {
            provider.ShouldNotBeNull();
            return marker;
        }));
        var response = await app.Client.PutAsJsonAsync(
            "/api/rules/rules/sample",
            new { document = DocumentReferencing("customer.is-active"), baseVersion = 1 });

        // Assert — the store the factory returned is the very one the RuleSet published through
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var history = await marker.HistoryAsync("sample", default);
        history.ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Should_resolve_a_proposition_store_built_from_the_container()
    {
        // Arrange
        var marker = new InMemoryPropositionStore();

        // Act
        await using var app = TestApp.Create(builder => builder.AddPropositions(_ => marker));
        var response = await app.Client.PostAsJsonAsync("/api/rules/propositions", new
        {
            name = "customer.derived",
            modelType = "customer",
            document = DocumentReferencing("customer.is-active"),
            description = (string?)null,
        });

        // Assert — the authored proposition landed in the very store the factory returned
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        marker.Load().ShouldContain(p => p.Name == "customer.derived");
    }

    [Fact]
    public void Should_still_refuse_a_second_call_through_the_factory_overload()
    {
        // Arrange — the called-twice guard must not be bypassable by picking the other overload
        var act = () => TestApp.Create(builder =>
        {
            builder.AddRuleStore(new InMemoryRuleStore());
            builder.AddRuleStore(_ => new InMemoryRuleStore());
        });

        // Act & Assert
        act.ShouldThrow<InvalidOperationException>();
    }

    [Fact]
    public void Should_still_refuse_a_second_AddPropositions_call_through_the_factory_overload()
    {
        // Arrange — AddPropositions guards independently of AddRuleStore, so the same bypass
        // attempt needs its own coverage on that surface
        var act = () => TestApp.Create(builder =>
        {
            builder.AddPropositions(new InMemoryPropositionStore());
            builder.AddPropositions(_ => new InMemoryPropositionStore());
        });

        // Act & Assert
        act.ShouldThrow<InvalidOperationException>();
    }
}
