using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Rules;

public class RuleSetLoadTests
{
    private sealed class Customer
    {
        public bool IsActive { get; set; }
    }

    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class SampleRule() : Rule<Customer, string>("sample", IsActive);

    private const string Document = """{ "rule": { "spec": "customer.is-active" } }""";
    private const string Unbindable = """{ "rule": { "spec": "customer.was-renamed-away" } }""";

    private static StoredRuleVersion Row(int version, string? documentJson) =>
        new("sample", version, documentJson, "alice", DateTimeOffset.UnixEpoch, null, null, "test");

    private static async Task<(RuleSet Set, SampleRule Rule, RuleLoadReport Report)> Loaded(
        params StoredRuleVersion[] rows)
    {
        var store = new InMemoryRuleStore();
        foreach (var row in rows)
            (await store.AppendAsync([row], default)).IsConflict.ShouldBeFalse();

        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var rule = new SampleRule();
        var set = new RuleSet(registry, store).Add(rule);
        return (set, rule, set.Load());
    }

    [Fact]
    public async Task Should_apply_a_stored_document_over_the_compiled_default()
    {
        // Act
        var (_, rule, report) = await Loaded(Row(2, Document));

        // Assert — a stored document always beats the compiled default
        rule.DocumentJson!.ShouldBe(Document);
        rule.Version.ShouldBe(2);
        report.Quarantined.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_restore_the_stored_version_so_the_next_save_does_not_conflict()
    {
        // Act
        var (set, _, _) = await Loaded(Row(7, Document));

        // Assert
        set.FindEntry("sample")!.Version.ShouldBe(7);
    }

    [Fact]
    public async Task Should_apply_a_null_document_as_a_recorded_revert()
    {
        // Arrange — v2 authored, v3 reverted to code: the head is null, at version 3
        // Act
        var (set, rule, report) = await Loaded(Row(2, Document), Row(3, null));

        // Assert — back on the compiled default, but the version records that it happened
        rule.DocumentJson.ShouldBeNull();
        set.FindEntry("sample")!.Version.ShouldBe(3);
        report.Quarantined.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_quarantine_a_stored_document_that_no_longer_binds()
    {
        // Act — the spec the document references was renamed away by a redeploy
        var (set, _, report) = await Loaded(Row(2, Unbindable));

        // Assert — reported, never silent
        report.Quarantined.ShouldHaveSingleItem();
        report.Quarantined[0].Name.ShouldBe("sample");
        report.Quarantined[0].Errors.ShouldNotBeEmpty();
        set.FindEntry("sample")!.Quarantine.ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Should_keep_a_quarantined_rule_evaluable_on_its_compiled_default()
    {
        // Act
        var (_, rule, _) = await Loaded(Row(2, Unbindable));

        // Assert — a rule must be able to evaluate; there is nothing else to bind
        Should.NotThrow(() => rule.Evaluate(new Customer { IsActive = true }));
    }

    [Fact]
    public async Task Should_preserve_a_quarantined_rules_stored_version_for_repair()
    {
        // Act
        var (set, _, _) = await Loaded(Row(5, Unbindable));

        // Assert — an editor repairing it must send baseVersion 5, not 1
        set.FindEntry("sample")!.Version.ShouldBe(5);
    }

    [Fact]
    public async Task Should_clear_the_quarantine_once_the_rule_is_repaired()
    {
        // Arrange — booted quarantined, on its compiled default
        var (set, _, _) = await Loaded(Row(5, Unbindable));
        set.FindEntry("sample")!.Quarantine.ShouldNotBeEmpty();

        // Act — an editor repairs it, addressing the version the store holds
        var result = set.Update("sample", Document, 5);

        // Assert — the rule is no longer running a default in place of a broken stored document, so
        // the catalog must stop reporting that it is
        result.Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        set.FindEntry("sample")!.Quarantine.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_throw_on_demand_so_a_host_can_fail_fast()
    {
        // Arrange
        var (_, _, report) = await Loaded(Row(2, Unbindable));

        // Act / Assert — fail-fast is the host's policy, and the SDK supplies only the mechanism
        var exception = Should.Throw<RuleSerializationException>(() => report.ThrowIfQuarantined());
        exception.Message.ShouldContain("sample");
    }

    [Fact]
    public async Task Should_not_throw_when_nothing_was_quarantined()
    {
        // Arrange
        var (_, _, report) = await Loaded(Row(2, Document));

        // Act / Assert
        Should.NotThrow(() => report.ThrowIfQuarantined());
    }

    [Fact]
    public void Should_refuse_a_second_load()
    {
        // Arrange
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var set = new RuleSet(registry, new InMemoryRuleStore()).Add(new SampleRule());
        set.Load();

        // Act / Assert — Load reads the store once, at startup; a refresh is a whole rebuild
        Should.Throw<InvalidOperationException>(() => set.Load());
    }

    [Fact]
    public async Task Should_ignore_a_stored_rule_no_longer_registered_in_code()
    {
        // Arrange — a rule was deleted from the host, but its rows remain
        var store = new InMemoryRuleStore();
        await store.AppendAsync([new StoredRuleVersion(
            "retired", 1, Document, "alice", DateTimeOffset.UnixEpoch, null, null, "test")], default);

        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var set = new RuleSet(registry, store).Add(new SampleRule());

        // Act
        var report = set.Load();

        // Assert — an orphan row is not a quarantine: nothing is wrong with the document, the code
        // simply no longer declares the rule. The row is kept for history.
        report.Quarantined.ShouldBeEmpty();
        report.Orphaned.ShouldBe(["retired"]);
    }

    [Fact]
    public void Should_load_cleanly_with_an_empty_store()
    {
        // Arrange
        var registry = new SpecRegistry().Register("customer.is-active", IsActive);
        var rule = new SampleRule();
        var set = new RuleSet(registry, new InMemoryRuleStore()).Add(rule);

        // Act — a first boot is not an error
        var report = set.Load();

        // Assert
        report.Quarantined.ShouldBeEmpty();
        report.Orphaned.ShouldBeEmpty();
        rule.Version.ShouldBe(1);
        rule.DocumentJson.ShouldBeNull();
    }
}
