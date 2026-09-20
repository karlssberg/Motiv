using System.Text.Json;
using Shouldly;
using Xunit;

namespace Motiv.Serialization.Tests.Decisions;

/// <summary>
/// A logged decision run again under the documents that decided it. Every anchor the record pins —
/// rule version, build, proposition versions — is honoured or named in the fidelity verdict, and the
/// replay never records.
/// </summary>
[Collection(Diagnostics.RulesTelemetryTestCollection.Name)]
public class DecisionReproducerTests
{
    private sealed record Customer(string Id, bool IsActive, int Age);

    private static PolicyBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();
    private static PolicyBase<Customer, string> IsAdult { get; } =
        Spec.Build((Customer c) => c.Age >= 18).WhenTrue("adult").WhenFalse("minor").Create();
    private static AsyncPolicyBase<Customer, string> IsActiveAsync { get; } =
        Spec.BuildAsync((Customer c) => new ValueTask<bool>(c.IsActive)).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class CanCheckout() : Rule<Customer, string>("can-checkout", IsActive);
    private sealed class CanCheckoutAsync() : AsyncRule<Customer, string>("can-checkout-async", IsActiveAsync);
    private sealed class CanCheckoutPolicy() : PolicyRule<Customer, string>("can-checkout-policy", IsActive);

    private const string AuditedOverEligible = """{ "audited": true, "rule": { "spec": "customer.eligible" } }""";
    private const string EligibleIsActive = """{ "rule": { "spec": "customer.is-active" } }""";
    private const string EligibleIsAdult = """{ "rule": { "spec": "customer.is-adult" } }""";
    private const string AuditedOverIsActive = """{ "audited": true, "rule": { "spec": "customer.is-active" } }""";
    private const string AuditedOverIsActiveAsync = """{ "audited": true, "rule": { "spec": "customer.is-active-async" } }""";

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private sealed class Host : IAsyncDisposable
    {
        public required DecisionLog Log { get; init; }
        public required InMemoryDecisionSink Sink { get; init; }
        public required RuleSet Rules { get; init; }
        public required PropositionSet Propositions { get; init; }
        public required InMemoryRuleStore RuleStore { get; init; }
        public required InMemoryPropositionStore PropositionStore { get; init; }
        public required DecisionLogOptions Options { get; init; }
        public DecisionReproducer Reproducer() =>
            new(Sink, RuleStore, PropositionStore, Rules, Propositions, Options.Resolve, Web);

        /// <summary>Evaluates an audited rule and waits for the log's writer to hand the record to the sink.</summary>
        public async Task<DecisionRecord> DecideAsync(Customer customer, string rule = "can-checkout")
        {
            var before = Sink.Records.Count;
            switch (Rules.Find(rule))
            {
                case CanCheckoutAsync asyncRule: await asyncRule.EvaluateAsync(customer); break;
                case CanCheckoutPolicy policy: policy.Evaluate(customer); break;
                case CanCheckout sync: sync.Evaluate(customer); break;
                default: throw new InvalidOperationException($"unknown rule '{rule}'");
            }
            var deadline = DateTime.UtcNow.AddSeconds(5);
            while (Sink.Records.Count == before && DateTime.UtcNow < deadline)
                await Task.Delay(10);
            return Sink.Records[Sink.Records.Count - 1];
        }

        public async ValueTask DisposeAsync() => await Log.DisposeAsync();
    }

    private static async Task<Host> AHostAsync(Action<DecisionLogOptions>? configure = null)
    {
        var sink = new InMemoryDecisionSink();
        var options = new DecisionLogOptions { Backpressure = DecisionBackpressure.Block };
        if (configure is null)
            options.Capture.StoreWhole<Customer>();
        else
            configure(options);
        var log = new DecisionLog(sink, options);

        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.is-adult", IsAdult)
            .Register("customer.is-active-async", IsActiveAsync);
        var propositionStore = new InMemoryPropositionStore();
        var propositions = new PropositionSet(registry, propositionStore).AddModel<Customer>("customer");
        propositions.Load();
        var ruleStore = new InMemoryRuleStore();
        var rules = new RuleSet(propositions, ruleStore, decisionLog: log)
            .Add(new CanCheckout()).Add(new CanCheckoutAsync()).Add(new CanCheckoutPolicy());

        (await propositions.CreateAsync("customer.eligible", "customer", EligibleIsActive, null)).Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        var alice = new RuleChangeProvenance("alice");
        (await rules.UpdateAsync("can-checkout", AuditedOverEligible, 1, alice)).Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        (await rules.UpdateAsync("can-checkout-async", AuditedOverIsActiveAsync, 1, alice)).Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        (await rules.UpdateAsync("can-checkout-policy", AuditedOverIsActive, 1, alice)).Outcome.ShouldBe(RuleUpdateOutcome.Updated);

        return new Host
        {
            Log = log, Sink = sink, Rules = rules, Propositions = propositions,
            RuleStore = ruleStore, PropositionStore = propositionStore, Options = options,
        };
    }

    [Fact]
    public async Task Should_reproduce_an_exact_decision_with_the_model_and_the_same_verdict()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", IsActive: true, Age: 30));

        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        reproduction.Fidelity.IsExact.ShouldBeTrue(string.Join("; ", reproduction.Fidelity.Notes));
        reproduction.Rule!.Version.ShouldBe(2);
        reproduction.Propositions.Select(p => (p.Name, p.Version)).ShouldBe([("customer.eligible", 1)]);
        reproduction.Model.Kind.ShouldBe(ReproducedModelKind.Whole);
        reproduction.Replayed!.Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_bind_a_referenced_proposition_at_its_pinned_version_not_the_head()
    {
        // Arrange — decide under eligible v1 (is-active), then publish v2 (is-adult); the customer is an active minor
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-7", IsActive: true, Age: 16));
        (await host.Propositions.UpdateAsync("customer.eligible", EligibleIsAdult, 1)).Outcome.ShouldBe(PropositionUpdateOutcome.Updated);

        // Act
        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        // Assert — v1 said yes (active); the head would say no (minor)
        reproduction.Propositions.ShouldHaveSingleItem().Version.ShouldBe(1);
        reproduction.Replayed!.Satisfied.ShouldBeTrue();
        reproduction.Fidelity.IsExact.ShouldBeTrue(string.Join("; ", reproduction.Fidelity.Notes));
    }

    [Fact]
    public async Task Should_report_a_missing_proposition_version_and_fall_back_to_the_head()
    {
        // Arrange — a record pinning a version the log never held, as retention or a pre-log store would leave
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        var forged = decision with { Id = Guid.NewGuid(), ReferencedPropositionVersions = [new PropositionVersion("customer.eligible", 9)] };
        await host.Sink.WriteAsync([forged], default);

        var reproduction = await host.Reproducer().ReproduceAsync(forged.Id, default);

        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.PropositionVersionMissing && n.Detail.Contains("customer.eligible"));
        reproduction.Propositions.ShouldHaveSingleItem().Version.ShouldBe(1);
        reproduction.Replayed.ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_replay_a_reverted_version_against_the_compiled_default()
    {
        // Arrange — the audited document is v2; a revert makes v3 a null-document row
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        (await host.Rules.RevertAsync("can-checkout", 2, new RuleChangeProvenance("alice"))).Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        var forged = decision with { Id = Guid.NewGuid(), RuleVersion = 3, ReferencedPropositionVersions = [] };
        await host.Sink.WriteAsync([forged], default);

        var reproduction = await host.Reproducer().ReproduceAsync(forged.Id, default);

        reproduction.Rule!.DocumentJson.ShouldBeNull();
        reproduction.Replayed!.Satisfied.ShouldBeTrue();
        reproduction.Fidelity.Notes.ShouldNotContain(n => n.Reason == FidelityReason.RuleVersionMissing);
    }

    [Fact]
    public async Task Should_report_a_missing_rule_version_and_not_replay()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        var forged = decision with { Id = Guid.NewGuid(), RuleVersion = 42 };
        await host.Sink.WriteAsync([forged], default);

        var reproduction = await host.Reproducer().ReproduceAsync(forged.Id, default);

        reproduction.Rule.ShouldBeNull();
        reproduction.Replayed.ShouldBeNull();
        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.RuleVersionMissing);
    }

    [Fact]
    public async Task Should_report_a_build_mismatch()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        var forged = decision with { Id = Guid.NewGuid(), BuildId = "some-other-build" };
        await host.Sink.WriteAsync([forged], default);

        var reproduction = await host.Reproducer().ReproduceAsync(forged.Id, default);

        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.BuildMismatch && n.Detail.Contains("some-other-build"));
    }

    [Fact]
    public async Task Should_resolve_a_reference_only_capture_through_the_resolver()
    {
        await using var host = await AHostAsync(o =>
        {
            o.Capture.ReferenceOnly<Customer>(c => c.Id);
            o.Resolve.Reference<Customer>((key, _) => Task.FromResult<Customer?>(new Customer(key, true, 30)));
        });
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));

        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        reproduction.Model.Kind.ShouldBe(ReproducedModelKind.Resolved);
        reproduction.Model.Key!.ShouldBe("cust-42");
        reproduction.Replayed!.Satisfied.ShouldBeTrue();
        reproduction.Fidelity.IsExact.ShouldBeTrue(string.Join("; ", reproduction.Fidelity.Notes));
    }

    [Fact]
    public async Task Should_report_an_erased_subject_as_unresolved()
    {
        await using var host = await AHostAsync(o =>
        {
            o.Capture.ReferenceOnly<Customer>(c => c.Id);
            o.Resolve.Reference<Customer>((_, _) => Task.FromResult<Customer?>(null));
        });
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));

        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        reproduction.Model.Kind.ShouldBe(ReproducedModelKind.Reference);
        reproduction.Model.Key!.ShouldBe("cust-42");
        reproduction.Replayed.ShouldBeNull();
        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.ModelUnresolved);
    }

    [Fact]
    public async Task Should_report_a_reference_with_no_resolver_as_unresolved()
    {
        await using var host = await AHostAsync(o => o.Capture.ReferenceOnly<Customer>(c => c.Id));
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));

        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.ModelUnresolved && n.Detail.Contains("no resolver"));
    }

    [Fact]
    public async Task Should_report_a_diverged_outcome_loudly()
    {
        // The resolver hands back today's customer, who is no longer active: the replay flips
        await using var host = await AHostAsync(o =>
        {
            o.Capture.ReferenceOnly<Customer>(c => c.Id);
            o.Resolve.Reference<Customer>((key, _) => Task.FromResult<Customer?>(new Customer(key, IsActive: false, 30)));
        });
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));

        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        reproduction.Replayed!.Satisfied.ShouldBeFalse();
        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.OutcomeDiverged);
    }

    [Fact]
    public async Task Should_mark_a_redacted_capture_and_still_replay_it()
    {
        await using var host = await AHostAsync(o => o.Capture.Redact<Customer>(c => new { c.Id, c.IsActive, c.Age }));
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));

        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        reproduction.Model.Kind.ShouldBe(ReproducedModelKind.Redacted);
        reproduction.Replayed!.Satisfied.ShouldBeTrue();
        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.ModelRedacted);
    }

    [Fact]
    public async Task Should_rehydrate_a_whole_capture_from_json()
    {
        // As the SQL sink hands it back: a JsonElement, not the model
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        var element = JsonSerializer.SerializeToElement(new Customer("cust-42", true, 30), Web);
        var fromSql = decision with { Id = Guid.NewGuid(), Input = DecisionInput.Whole(element) };
        await host.Sink.WriteAsync([fromSql], default);

        var reproduction = await host.Reproducer().ReproduceAsync(fromSql.Id, default);

        reproduction.Model.Value.ShouldBeOfType<Customer>();
        reproduction.Replayed!.Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_bind_pinned_propositions_that_reference_each_other()
    {
        // eligible -> vip -> is-active; the pin lists them in whatever order the pin resolver walked
        await using var host = await AHostAsync();
        (await host.Propositions.CreateAsync("customer.vip", "customer", EligibleIsActive, null)).Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        (await host.Propositions.UpdateAsync("customer.eligible", """{ "rule": { "spec": "customer.vip" } }""", 1)).Outcome.ShouldBe(PropositionUpdateOutcome.Updated);
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        decision.ReferencedPropositionVersions.Select(p => p.Name).ShouldBe(["customer.eligible", "customer.vip"], ignoreOrder: true);

        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        reproduction.Fidelity.IsExact.ShouldBeTrue(string.Join("; ", reproduction.Fidelity.Notes));
        reproduction.Replayed!.Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_leave_the_dependents_of_a_pinned_document_that_did_not_bind_unbound()
    {
        // Arrange — eligible v2 references vip; a vip v9 row written straight into the log names a
        // spec that does not exist, and a forged record pins both
        await using var host = await AHostAsync();
        (await host.Propositions.CreateAsync("customer.vip", "customer", EligibleIsActive, null)).Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        (await host.Propositions.UpdateAsync("customer.eligible", """{ "rule": { "spec": "customer.vip" } }""", 1)).Outcome.ShouldBe(PropositionUpdateOutcome.Updated);
        var broken = new StoredProposition("customer.vip", "customer", """{ "rule": { "spec": "customer.gone" } }""", 9, null);
        (await host.PropositionStore.WriteAsync(PropositionBatch.Save(broken), default)).IsConflict.ShouldBeFalse();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        var forged = decision with
        {
            Id = Guid.NewGuid(),
            ReferencedPropositionVersions = [new PropositionVersion("customer.eligible", 2), new PropositionVersion("customer.vip", 9)],
        };
        await host.Sink.WriteAsync([forged], default);

        // Act
        var reproduction = await host.Reproducer().ReproduceAsync(forged.Id, default);

        // Assert — vip failed on its own; eligible was never bound rather than resolved through the
        // live head; and the rule, which references eligible, did not replay against today's logic either
        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.PropositionBindFailed && n.Detail.StartsWith("'customer.vip' v9 did not bind"));
        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.PropositionBindFailed && n.Detail.Contains("'customer.eligible' v2 was not bound") && n.Detail.Contains("'customer.vip'"));
        reproduction.Replayed.ShouldBeNull();
    }

    [Fact]
    public async Task Should_replay_an_audited_document_on_a_host_without_a_decision_log()
    {
        // Arrange — the decision was logged here; the reproduction runs on a host that only reads
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        var registry = new SpecRegistry().Register("customer.is-active", IsActive).Register("customer.is-adult", IsAdult);
        var propositions = new PropositionSet(registry, host.PropositionStore).AddModel<Customer>("customer");
        propositions.Load();
        var readOnly = new RuleSet(propositions, host.RuleStore).Add(new CanCheckout());
        var reproducer = new DecisionReproducer(host.Sink, host.RuleStore, host.PropositionStore, readOnly, propositions, new DecisionModelResolvers(), Web);

        // Act
        var reproduction = await reproducer.ReproduceAsync(decision.Id, default);

        // Assert — an audited document records nothing on replay, so the capture gate does not apply
        reproduction.Fidelity.Notes.ShouldNotContain(n => n.Reason == FidelityReason.PropositionBindFailed);
        reproduction.Replayed!.Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_reproduce_a_decision_of_an_async_rule()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30), "can-checkout-async");

        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        reproduction.Fidelity.IsExact.ShouldBeTrue(string.Join("; ", reproduction.Fidelity.Notes));
        reproduction.Replayed!.Satisfied.ShouldBeTrue();
        reproduction.Replayed.Assertions.ShouldBe(["active"]);
    }

    [Fact]
    public async Task Should_reproduce_a_decision_of_a_policy_rule_with_its_single_value()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-9", IsActive: false, 41), "can-checkout-policy");

        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        reproduction.Fidelity.IsExact.ShouldBeTrue(string.Join("; ", reproduction.Fidelity.Notes));
        reproduction.Replayed!.Satisfied.ShouldBeFalse();
        reproduction.Replayed.Values.ShouldBe(["inactive"]);
    }

    [Fact]
    public async Task Should_note_every_pin_when_the_host_has_no_proposition_set()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        var withoutPropositions = new DecisionReproducer(
            host.Sink, host.RuleStore, host.PropositionStore, host.Rules, propositions: null, host.Options.Resolve, Web);

        var reproduction = await withoutPropositions.ReproduceAsync(decision.Id, default);

        reproduction.Propositions.ShouldBeEmpty();
        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.PropositionBindFailed && n.Detail.Contains("no proposition set"));
    }

    [Fact]
    public async Task Should_print_the_pinned_document_as_c_sharp()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));

        var reproduction = await host.Reproducer().ReproduceAsync(decision.Id, default);

        reproduction.CSharp.ShouldNotBeNull();
        reproduction.CSharp.ShouldContain("public static class CanCheckoutRule");
        reproduction.CSharp.ShouldContain("""return registry.Get<Customer>("customer.eligible");""");
        reproduction.CSharpWarnings.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_print_nothing_for_a_reverted_version()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        (await host.Rules.RevertAsync("can-checkout", 2, new RuleChangeProvenance("alice"))).Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        var forged = decision with { Id = Guid.NewGuid(), RuleVersion = 3, ReferencedPropositionVersions = [] };
        await host.Sink.WriteAsync([forged], default);

        var reproduction = await host.Reproducer().ReproduceAsync(forged.Id, default);

        reproduction.CSharp.ShouldBeNull();
        reproduction.CSharpWarnings.ShouldBeEmpty();
    }

    [Fact]
    public async Task Should_report_a_rule_the_host_no_longer_registers_and_produce_no_model()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        var forged = decision with { Id = Guid.NewGuid(), RuleName = "retired-rule" };
        await host.Sink.WriteAsync([forged], default);

        var reproduction = await host.Reproducer().ReproduceAsync(forged.Id, default);

        reproduction.Rule.ShouldBeNull();
        reproduction.Model.Kind.ShouldBe(ReproducedModelKind.Absent);
        reproduction.Replayed.ShouldBeNull();
        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.RuleVersionMissing && n.Detail.Contains("no rule 'retired-rule'"));
    }

    [Fact]
    public async Task Should_report_a_rule_document_that_does_not_bind_at_replay()
    {
        // Arrange — a stored version whose document references a spec no source knows
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        (await host.RuleStore.AppendAsync(
            [new StoredRuleVersion("can-checkout", 9, """{ "audited": true, "rule": { "spec": "customer.vanished" } }""", "alice", DateTimeOffset.MinValue, null, null, "test")],
            default)).IsConflict.ShouldBeFalse();
        var forged = decision with { Id = Guid.NewGuid(), RuleVersion = 9, ReferencedPropositionVersions = [] };
        await host.Sink.WriteAsync([forged], default);

        var reproduction = await host.Reproducer().ReproduceAsync(forged.Id, default);

        reproduction.Rule!.Version.ShouldBe(9);
        reproduction.Replayed.ShouldBeNull();
        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.PropositionBindFailed && n.Detail.Contains("v9 did not bind"));
    }

    [Fact]
    public async Task Should_report_a_pinned_document_that_does_not_parse()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        (await host.PropositionStore.WriteAsync(
            new PropositionBatch([new StoredProposition("customer.broken", "customer", "{ not json", 1, null)], []), default))
            .IsConflict.ShouldBeFalse();
        var forged = decision with { Id = Guid.NewGuid(), ReferencedPropositionVersions = [new PropositionVersion("customer.broken", 1)] };
        await host.Sink.WriteAsync([forged], default);

        var reproduction = await host.Reproducer().ReproduceAsync(forged.Id, default);

        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.PropositionBindFailed && n.Detail.Contains("'customer.broken' v1 does not parse"));
        reproduction.Replayed.ShouldBeNull();
    }

    [Fact]
    public async Task Should_report_a_pin_whose_name_has_no_head_to_stand_in()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        var forged = decision with { Id = Guid.NewGuid(), ReferencedPropositionVersions = [new PropositionVersion("customer.never-existed", 3)] };
        await host.Sink.WriteAsync([forged], default);

        var reproduction = await host.Reproducer().ReproduceAsync(forged.Id, default);

        reproduction.Propositions.ShouldBeEmpty();
        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.PropositionVersionMissing && n.Detail.Contains("no live head to stand in"));
    }

    [Fact]
    public async Task Should_report_nothing_captured_when_the_record_has_no_input()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        var forged = decision with { Id = Guid.NewGuid(), Input = null };
        await host.Sink.WriteAsync([forged], default);

        var reproduction = await host.Reproducer().ReproduceAsync(forged.Id, default);

        reproduction.Model.Kind.ShouldBe(ReproducedModelKind.Absent);
        reproduction.Replayed.ShouldBeNull();
        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.ModelUnresolved && n.Detail == "nothing was captured for this decision");
    }

    [Fact]
    public async Task Should_report_a_captured_model_that_was_null()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        var forged = decision with { Id = Guid.NewGuid(), Input = DecisionInput.Whole(null) };
        await host.Sink.WriteAsync([forged], default);

        var reproduction = await host.Reproducer().ReproduceAsync(forged.Id, default);

        reproduction.Model.Kind.ShouldBe(ReproducedModelKind.Whole);
        reproduction.Model.Value.ShouldBeNull();
        reproduction.Replayed.ShouldBeNull();
        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.ModelUnresolved && n.Detail == "the captured model was null");
    }

    [Fact]
    public async Task Should_report_a_capture_that_does_not_read_as_the_model()
    {
        // Arrange — a durable sink hands back a JsonElement; this one is a string, not a customer
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        var forged = decision with { Id = Guid.NewGuid(), Input = DecisionInput.Whole(JsonDocument.Parse("\"not a customer\"").RootElement) };
        await host.Sink.WriteAsync([forged], default);

        var reproduction = await host.Reproducer().ReproduceAsync(forged.Id, default);

        reproduction.Model.Value.ShouldBeNull();
        reproduction.Replayed.ShouldBeNull();
        reproduction.Fidelity.Notes.ShouldContain(n => n.Reason == FidelityReason.ModelUnresolved && n.Detail.Contains("could not be read as Customer"));
    }

    [Fact]
    public async Task Should_replay_without_a_proposition_set_when_nothing_is_pinned()
    {
        // Arrange — the policy rule's document references only compiled specs, so the replay binds with the rule set's own options
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30), "can-checkout-policy");
        var withoutPropositions = new DecisionReproducer(
            host.Sink, host.RuleStore, host.PropositionStore, host.Rules, propositions: null, host.Options.Resolve, Web);

        var reproduction = await withoutPropositions.ReproduceAsync(decision.Id, default);

        reproduction.Fidelity.IsExact.ShouldBeTrue(string.Join("; ", reproduction.Fidelity.Notes));
        reproduction.Replayed!.Satisfied.ShouldBeTrue();
    }

    [Fact]
    public async Task Should_replay_an_async_rule_reverted_to_its_compiled_default()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30), "can-checkout-async");
        (await host.Rules.RevertAsync("can-checkout-async", 2, new RuleChangeProvenance("alice"))).Outcome.ShouldBe(RuleUpdateOutcome.Updated);
        var forged = decision with { Id = Guid.NewGuid(), RuleVersion = 3, ReferencedPropositionVersions = [] };
        await host.Sink.WriteAsync([forged], default);

        var reproduction = await host.Reproducer().ReproduceAsync(forged.Id, default);

        reproduction.Rule!.DocumentJson.ShouldBeNull();
        reproduction.Replayed!.Satisfied.ShouldBeTrue();
        reproduction.Fidelity.IsExact.ShouldBeTrue(string.Join("; ", reproduction.Fidelity.Notes));
    }

    [Fact]
    public void Should_print_a_fidelity_note_as_its_reason_and_detail()
    {
        new FidelityNote(FidelityReason.BuildMismatch, "decided on build 'a'").ToString().ShouldBe("BuildMismatch: decided on build 'a'");
    }

    [Fact]
    public async Task Should_throw_for_an_unknown_decision()
    {
        await using var host = await AHostAsync();
        await Should.ThrowAsync<DecisionNotFoundException>(() => host.Reproducer().ReproduceAsync(Guid.NewGuid(), default));
    }

    [Fact]
    public async Task Should_never_record_a_replay()
    {
        await using var host = await AHostAsync();
        var decision = await host.DecideAsync(new Customer("cust-42", true, 30));
        var before = host.Sink.Records.Count;

        await host.Reproducer().ReproduceAsync(decision.Id, default);
        await Task.Delay(100);

        host.Sink.Records.Count.ShouldBe(before);
    }
}
