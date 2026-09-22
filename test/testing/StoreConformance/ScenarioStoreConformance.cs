using System;
using System.Linq;
using System.Threading.Tasks;
using Motiv.Serialization;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.Testing;

/// <summary>What it means to be an <see cref="IScenarioStore"/>, as one suite every implementation derives from.</summary>
public abstract class ScenarioStoreConformance : IAsyncLifetime
{
    /// <summary>The store under test. A fresh, empty one per test.</summary>
    protected IScenarioStore Store { get; private set; } = null!;

    /// <summary>Creates an empty store. Called once per test.</summary>
    protected abstract Task<IScenarioStore> CreateStoreAsync();

    /// <summary>Releases whatever <see cref="CreateStoreAsync"/> allocated. Does nothing by default.</summary>
    protected virtual Task DisposeStoreAsync() => Task.CompletedTask;

    /// <summary>One scenario row; the store ignores its version and stamps its timestamp.</summary>
    protected static StoredScenario Scenario(
        string rule, string id, string name = "Active adult", string model = """{ "age": 30 }""",
        bool? expected = null, string? sourceDecisionId = null, string author = "alice") =>
        new(rule, id, name, model, expected, sourceDecisionId, Version: 0, author, DateTimeOffset.MinValue);

    public async Task InitializeAsync() => Store = await CreateStoreAsync();

    public Task DisposeAsync() => DisposeStoreAsync();

    [Fact]
    public async Task Should_start_empty()
    {
        (await Store.LoadAsync(default)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_create_at_version_one_and_read_it_back_for_its_rule()
    {
        // Act
        var written = await Store.PutAsync(
            Scenario("can-checkout", "s1", expected: true, sourceDecisionId: "d-1"), baseVersion: 0, default);

        // Assert
        written.IsConflict.ShouldBeFalse();
        written.Version.ShouldBe(1);
        var row = (await Store.ForRuleAsync("can-checkout", default)).ShouldHaveSingleItem();
        row.Id.ShouldBe("s1");
        row.Name.ShouldBe("Active adult");
        row.ModelJson.ShouldBe("""{ "age": 30 }""");
        row.ExpectedSatisfied.ShouldBe(true);
        row.SourceDecisionId!.ShouldBe("d-1");
        row.Version.ShouldBe(1);
        row.Author.ShouldBe("alice");
        (await Store.ForRuleAsync("other", default)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_stamp_the_timestamp_on_write()
    {
        var before = DateTimeOffset.UtcNow.AddSeconds(-1);
        await Store.PutAsync(Scenario("r", "s1"), 0, default);
        var row = (await Store.ForRuleAsync("r", default)).ShouldHaveSingleItem();
        row.TimestampUtc.ShouldBeGreaterThanOrEqualTo(before);
        row.TimestampUtc.ShouldBeLessThanOrEqualTo(DateTimeOffset.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task Should_update_at_the_stored_version_and_move_it_forward()
    {
        await Store.PutAsync(Scenario("r", "s1"), 0, default);

        var written = await Store.PutAsync(Scenario("r", "s1", name: "Renamed", author: "bob"), baseVersion: 1, default);

        written.IsConflict.ShouldBeFalse();
        written.Version.ShouldBe(2);
        var row = (await Store.ForRuleAsync("r", default)).ShouldHaveSingleItem();
        row.Name.ShouldBe("Renamed");
        row.Version.ShouldBe(2);
        row.Author.ShouldBe("bob");
    }

    [Fact]
    public async Task Should_refuse_a_put_at_a_stale_version()
    {
        await Store.PutAsync(Scenario("r", "s1"), 0, default);
        await Store.PutAsync(Scenario("r", "s1", name: "v2"), 1, default);

        var stale = await Store.PutAsync(Scenario("r", "s1", name: "stale"), baseVersion: 1, default);

        stale.IsConflict.ShouldBeTrue();
        stale.CurrentVersion.ShouldBe(2);
        (await Store.ForRuleAsync("r", default)).ShouldHaveSingleItem().Name.ShouldBe("v2");
    }

    [Fact]
    public async Task Should_refuse_creating_an_id_the_rule_already_holds()
    {
        await Store.PutAsync(Scenario("r", "s1"), 0, default);

        var again = await Store.PutAsync(Scenario("r", "s1", name: "again"), baseVersion: 0, default);

        again.IsConflict.ShouldBeTrue();
        again.CurrentVersion.ShouldBe(1);
    }

    [Fact]
    public async Task Should_refuse_updating_an_absent_id()
    {
        var missing = await Store.PutAsync(Scenario("r", "nope"), baseVersion: 3, default);

        missing.IsConflict.ShouldBeTrue();
        missing.CurrentVersion.ShouldBe(0);
    }

    [Fact]
    public async Task Should_scope_ids_to_the_rule()
    {
        await Store.PutAsync(Scenario("a", "s1"), 0, default);

        var other = await Store.PutAsync(Scenario("b", "s1"), 0, default);

        other.IsConflict.ShouldBeFalse();
        (await Store.LoadAsync(default)).Count.ShouldBe(2);
    }

    [Fact]
    public async Task Should_delete_at_the_stored_version()
    {
        await Store.PutAsync(Scenario("r", "s1"), 0, default);

        var deleted = await Store.DeleteAsync("r", "s1", baseVersion: 1, default);

        deleted.IsConflict.ShouldBeFalse();
        (await Store.ForRuleAsync("r", default)).ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_refuse_deleting_at_a_stale_version_or_an_absent_id()
    {
        await Store.PutAsync(Scenario("r", "s1"), 0, default);

        (await Store.DeleteAsync("r", "s1", baseVersion: 2, default)).IsConflict.ShouldBeTrue();
        (await Store.DeleteAsync("r", "s1", baseVersion: 0, default)).IsConflict.ShouldBeTrue();
        var absent = await Store.DeleteAsync("r", "nope", baseVersion: 1, default);
        absent.IsConflict.ShouldBeTrue();
        absent.CurrentVersion.ShouldBe(0);
        (await Store.ForRuleAsync("r", default)).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task Should_keep_the_model_text_verbatim_even_when_it_is_not_json()
    {
        // Studio keeps a half-typed model as typed; the store is not the place to reject it
        const string halfTyped = "{ \"age\": 3";
        await Store.PutAsync(Scenario("r", "s1", model: halfTyped), 0, default);

        (await Store.ForRuleAsync("r", default)).ShouldHaveSingleItem().ModelJson.ShouldBe(halfTyped);
    }

    [Fact]
    public async Task Should_list_a_rules_scenarios_in_the_order_they_were_added()
    {
        await Store.PutAsync(Scenario("r", "b", name: "second"), 0, default);
        await Store.PutAsync(Scenario("r", "a", name: "first-by-id-but-added-later"), 0, default);

        (await Store.ForRuleAsync("r", default)).Select(s => s.Name).ShouldBe(["second", "first-by-id-but-added-later"]);
    }
}
