namespace Motiv.Serialization.Tests.Decisions;

/// <summary>
/// Recording, across all four evaluation entry points. Two of the four —
/// <see cref="PolicyRule{TModel,TMetadata}.Evaluate"/> and
/// <see cref="AsyncPolicyRule{TModel,TMetadata}.EvaluateAsync"/> — are <em>shadows</em> rather than
/// overrides, which is exactly the shape that gets missed, and a missed one is a rule that says it is
/// audited and is not.
/// </summary>
public class DecisionRecordingTests
{
    private sealed class Customer(string id, bool isActive)
    {
        public string Id { get; } = id;
        public bool IsActive { get; } = isActive;
    }

    private static PolicyBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private static AsyncPolicyBase<Customer, string> IsActiveAsync { get; } =
        Spec.BuildAsync((Customer c) => new ValueTask<bool>(c.IsActive))
            .WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class SpecFlavoured() : Rule<Customer, string>("spec-flavoured", IsActive);
    private sealed class PolicyFlavoured() : PolicyRule<Customer, string>("policy-flavoured", IsActive);
    private sealed class AsyncSpecFlavoured() : AsyncRule<Customer, string>("async-spec", IsActiveAsync);
    private sealed class AsyncPolicyFlavoured() : AsyncPolicyRule<Customer, string>("async-policy", IsActiveAsync);

    private const string AuditedDocument =
        """{ "audited": true, "rule": { "spec": "customer.is-active" } }""";
    private const string PlainDocument =
        """{ "rule": { "spec": "customer.is-active" } }""";

    private static readonly Customer Alice = new("cust-42", isActive: true);

    /// <summary>The four flavours, each with the way its own entry point is reached.</summary>
    public static TheoryData<string> Flavours =>
        ["spec-flavoured", "policy-flavoured", "async-spec", "async-policy"];

    private sealed class Host : IAsyncDisposable
    {
        public required DecisionLog Log { get; init; }
        public required InMemoryDecisionSink Sink { get; init; }
        public required RuleSet Rules { get; init; }

        public async ValueTask DisposeAsync() => await Log.DisposeAsync();

        /// <summary>Reaches the entry point of the named flavour, awaiting the async ones.</summary>
        public async Task<bool> EvaluateAsync(string flavour, Customer customer) =>
            Rules.Find(flavour) switch
            {
                AsyncPolicyFlavoured rule => (await rule.EvaluateAsync(customer)).Satisfied,
                AsyncSpecFlavoured rule => (await rule.EvaluateAsync(customer)).Satisfied,
                PolicyFlavoured rule => rule.Evaluate(customer).Satisfied,
                SpecFlavoured rule => rule.Evaluate(customer).Satisfied,
                _ => throw new InvalidOperationException($"unknown flavour '{flavour}'")
            };

        /// <summary>Drains the writer so the sink can be read, then reopens the log for further use.</summary>
        public async Task<IReadOnlyList<DecisionRecord>> DrainAsync()
        {
            await Log.DisposeAsync();
            return Sink.Records;
        }
    }

    private static async Task<Host> AHostAsync(
        string document = AuditedDocument,
        DecisionBackpressure backpressure = DecisionBackpressure.FailClosed,
        int capacity = 64,
        bool withCapture = true)
    {
        var sink = new InMemoryDecisionSink();
        var options = new DecisionLogOptions { Backpressure = backpressure, QueueCapacity = capacity };
        if (withCapture)
            options.Capture.ReferenceOnly<Customer>(c => c.Id);

        var log = new DecisionLog(sink, options);
        var registry = new SpecRegistry()
            .Register("customer.is-active", IsActive)
            .Register("customer.is-active-async", IsActiveAsync);

        var rules = new RuleSet(registry, decisionLog: log)
            .Add(new SpecFlavoured())
            .Add(new PolicyFlavoured())
            .Add(new AsyncSpecFlavoured())
            .Add(new AsyncPolicyFlavoured());

        foreach (var name in new[] { "spec-flavoured", "policy-flavoured" })
            (await rules.UpdateAsync(name, document, 1, new RuleChangeProvenance("alice")))
                .Outcome.ShouldBe(RuleUpdateOutcome.Updated);

        var asyncDocument = document.Replace("customer.is-active", "customer.is-active-async");
        foreach (var name in new[] { "async-spec", "async-policy" })
            (await rules.UpdateAsync(name, asyncDocument, 1, new RuleChangeProvenance("alice")))
                .Outcome.ShouldBe(RuleUpdateOutcome.Updated);

        return new Host { Log = log, Sink = sink, Rules = rules };
    }

    [Theory]
    [MemberData(nameof(Flavours))]
    public async Task Should_record_one_decision_per_evaluation_of_an_audited_rule(string flavour)
    {
        // Arrange
        await using var host = await AHostAsync();

        // Act
        await host.EvaluateAsync(flavour, Alice);
        await host.EvaluateAsync(flavour, Alice);
        var records = await host.DrainAsync();

        // Assert — audited means total, not sampled
        records.Count(record => record.RuleName == flavour).ShouldBe(2);
    }

    [Theory]
    [MemberData(nameof(Flavours))]
    public async Task Should_record_nothing_for_an_unaudited_rule(string flavour)
    {
        // Arrange
        await using var host = await AHostAsync(PlainDocument);

        // Act
        await host.EvaluateAsync(flavour, Alice);
        var records = await host.DrainAsync();

        // Assert
        records.ShouldBeEmpty();
    }

    [Theory]
    [MemberData(nameof(Flavours))]
    public async Task Should_pin_the_rule_version_and_the_build(string flavour)
    {
        // Arrange
        await using var host = await AHostAsync();

        // Act
        await host.EvaluateAsync(flavour, Alice);
        var record = (await host.DrainAsync()).ShouldHaveSingleItem();

        // Assert — anchors 1 and 2
        record.RuleName.ShouldBe(flavour);
        record.RuleVersion.ShouldBe(2);
        record.BuildId.ShouldBe(BuildIdentity.Current);
    }

    [Theory]
    [MemberData(nameof(Flavours))]
    public async Task Should_capture_the_input_under_the_configured_posture(string flavour)
    {
        // Arrange
        await using var host = await AHostAsync();

        // Act
        await host.EvaluateAsync(flavour, Alice);
        var record = (await host.DrainAsync()).ShouldHaveSingleItem();

        // Assert — the customer's key and nothing else of them
        var input = record.Input.ShouldNotBeNull();
        input.Kind.ShouldBe(DecisionInputKind.Reference);
        input.Value.ShouldBe("cust-42");
    }

    [Theory]
    [MemberData(nameof(Flavours))]
    public async Task Should_record_the_outcome_the_caller_was_given(string flavour)
    {
        // Arrange
        await using var host = await AHostAsync();

        // Act
        var satisfied = await host.EvaluateAsync(flavour, Alice);
        var record = (await host.DrainAsync()).ShouldHaveSingleItem();

        // Assert — the record and the response must not be able to describe one evaluation differently
        record.Outcome.Satisfied.ShouldBe(satisfied);
        record.Outcome.Reason.ShouldBe("active");
        record.Outcome.Assertions.ShouldBe(["active"]);
        record.Outcome.Justification.ShouldNotBeNullOrWhiteSpace();
        record.Outcome.Explanation.Assertions.ShouldBe(["active"]);
    }

    [Theory]
    [MemberData(nameof(Flavours))]
    public async Task Should_stamp_a_correlation_id_and_caller_when_a_decision_is_open(string flavour)
    {
        // Arrange
        await using var host = await AHostAsync();

        // Act
        using (host.Rules.PinSnapshot("corr-7", caller: "alice"))
            await host.EvaluateAsync(flavour, Alice);
        var record = (await host.DrainAsync()).ShouldHaveSingleItem();

        // Assert
        record.CorrelationId.ShouldBe("corr-7");
        record.Caller!.ShouldBe("alice");
    }

    [Fact]
    public async Task Should_give_every_rule_of_one_decision_the_same_correlation_id()
    {
        // Arrange
        await using var host = await AHostAsync();

        // Act — the checkout shape: several rules, one decision
        using (host.Rules.PinSnapshot("corr-checkout"))
        {
            await host.EvaluateAsync("spec-flavoured", Alice);
            await host.EvaluateAsync("async-policy", Alice);
        }
        var records = await host.DrainAsync();

        // Assert
        records.Count.ShouldBe(2);
        records.Select(record => record.CorrelationId).Distinct().ShouldHaveSingleItem()
            .ShouldBe("corr-checkout");
    }

    [Fact]
    public async Task Should_give_two_unpinned_evaluations_two_correlation_ids()
    {
        // Arrange
        await using var host = await AHostAsync();

        // Act
        await host.EvaluateAsync("spec-flavoured", Alice);
        await host.EvaluateAsync("spec-flavoured", Alice);
        var records = await host.DrainAsync();

        // Assert — two decisions, not one, and each still findable
        records.Select(record => record.CorrelationId).Distinct().Count().ShouldBe(2);
        records.ShouldAllBe(record => record.Caller == null);
    }

    [Theory]
    [MemberData(nameof(Flavours))]
    public async Task Should_fail_the_evaluation_when_the_record_cannot_be_queued(string flavour)
    {
        // Arrange — a log already closed is a queue that will never accept anything again
        var host = await AHostAsync(capacity: 1);
        await host.Log.DisposeAsync();

        // Act / Assert — the caller gets no result: an audited decision that was not logged did not
        // happen
        var act = async () => await host.EvaluateAsync(flavour, Alice);
        (await act.ShouldThrowAsync<DecisionNotLoggedException>()).RuleName.ShouldBe(flavour);
    }

    [Theory]
    [MemberData(nameof(Flavours))]
    public async Task Should_fail_every_posture_once_the_log_is_disposed(string flavour)
    {
        // Arrange — Drop, whose whole contract is that the evaluation proceeds
        var host = await AHostAsync(backpressure: DecisionBackpressure.Drop, capacity: 1);
        await host.Log.DisposeAsync();

        // Act / Assert — a disposed log cannot write the gap marker that makes a drop provable, so
        // dropping here would be the silent loss Drop exists to avoid. Disposal is a lifecycle event,
        // not backpressure, and Block would hang on capacity that is never coming back.
        var act = async () => await host.EvaluateAsync(flavour, Alice);
        (await act.ShouldThrowAsync<DecisionNotLoggedException>()).Message
            .ShouldContain("disposed");
    }

    [Theory]
    [MemberData(nameof(Flavours))]
    public async Task Should_still_return_a_result_when_a_live_log_sheds_under_drop(string flavour)
    {
        // Arrange — a live log whose queue cannot hold what is coming
        await using var host = await AHostAsync(backpressure: DecisionBackpressure.Drop, capacity: 1);

        // Act
        var satisfied = true;
        for (var i = 0; i < 200; i++)
            satisfied &= await host.EvaluateAsync(flavour, Alice);

        // Assert — Drop protects latency, so no evaluation fails...
        satisfied.ShouldBeTrue();

        // ...and what it shed is counted rather than forgotten
        await host.Log.DisposeAsync();
        (host.Sink.Records.Count + (int)host.Log.DroppedCount).ShouldBe(200);
    }
}
