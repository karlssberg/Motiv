using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Propositions;

public class PropositionSetAsyncWriteTests
{
    private static SpecBase<Customer, string> IsActive { get; } =
        Spec.Build((Customer c) => c.IsActive).WhenTrue("active").WhenFalse("inactive").Create();

    private sealed class SampleRule() : Rule<Customer, string>("sample", IsActive);

    private const string Document = """{ "rule": { "spec": "customer.is-active" } }""";

    /// <summary>A store that refuses every write — the "persist failed" arm.</summary>
    private sealed class FailingPropositionStore : IPropositionStore
    {
        public IReadOnlyList<StoredProposition> Load() => [];
        public Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<StoredProposition>>([]);
        public Task<long> GetGenerationAsync(CancellationToken ct) => Task.FromResult(0L);

        public Task WriteAsync(PropositionBatch batch, CancellationToken cancellationToken) =>
            throw new IOException("disk full");
    }

    /// <summary>Records when a write enters and leaves the store, so an interleave is observable.</summary>
    private sealed class TracingPropositionStore(List<string> timeline) : IPropositionStore
    {
        private readonly InMemoryPropositionStore _inner = new();

        public IReadOnlyList<StoredProposition> Load() => _inner.Load();
        public Task<IReadOnlyList<StoredProposition>> LoadAsync(CancellationToken ct) => _inner.LoadAsync(ct);
        public Task<long> GetGenerationAsync(CancellationToken ct) => _inner.GetGenerationAsync(ct);

        public async Task WriteAsync(PropositionBatch batch, CancellationToken cancellationToken)
        {
            lock (timeline) timeline.Add("proposition-enter");
            await Task.Yield();
            await _inner.WriteAsync(batch, cancellationToken);
            lock (timeline) timeline.Add("proposition-exit");
        }
    }

    /// <summary>The rule-side twin of <see cref="TracingPropositionStore"/>.</summary>
    private sealed class TracingRuleStore(List<string> timeline) : IRuleStore
    {
        private readonly InMemoryRuleStore _inner = new();

        public IReadOnlyList<StoredRule> Load() => _inner.Load();
        public Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken ct) => _inner.LoadAsync(ct);
        public Task<long> GetGenerationAsync(CancellationToken ct) => _inner.GetGenerationAsync(ct);
        public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(string n, CancellationToken ct) =>
            _inner.HistoryAsync(n, ct);

        public async Task<RuleAppendResult> AppendAsync(
            IReadOnlyList<StoredRuleVersion> versions, CancellationToken ct)
        {
            lock (timeline) timeline.Add("rule-enter");
            await Task.Yield();
            var result = await _inner.AppendAsync(versions, ct);
            lock (timeline) timeline.Add("rule-exit");
            return result;
        }
    }

    private static PropositionSet Fresh(IPropositionStore? store = null)
    {
        var scope = new BindingScope(new SpecRegistry().Register("customer.is-active", IsActive));
        var set = new PropositionSet(scope, store ?? new InMemoryPropositionStore())
            .AddModel<Customer>("customer");
        set.Load();
        return set;
    }

    [Fact]
    public async Task Should_create_through_the_async_path()
    {
        // Arrange
        var set = Fresh();

        // Act
        var result = await set.CreateAsync("customer.a", "customer", Document, null);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Created);
        set.Find("customer.a").ShouldNotBeNull();
    }

    [Fact]
    public async Task Should_leave_nothing_live_when_the_store_refuses_the_write()
    {
        // Arrange
        var set = Fresh(new FailingPropositionStore());

        // Act
        await Should.ThrowAsync<IOException>(async () =>
            await set.CreateAsync("customer.a", "customer", Document, null));

        // Assert — persist runs before any memory mutation, so a failure leaves nothing behind
        set.Find("customer.a").ShouldBeNull();
        set.DocumentJsonOf("customer.a").ShouldBeNull();
    }

    [Fact]
    public async Task Should_withdraw_through_the_async_path()
    {
        // Arrange
        var set = Fresh();
        await set.CreateAsync("customer.a", "customer", Document, null);

        // Act
        var result = await set.WithdrawAsync("customer.a", 1);

        // Assert
        result.Outcome.ShouldBe(PropositionUpdateOutcome.Removed);
        set.Find("customer.a").ShouldBeNull();
    }

    [Fact]
    public async Task Should_write_a_save_and_a_delete_as_one_batch()
    {
        // Arrange — the batch shape is what makes an envelope all-or-nothing
        var store = new InMemoryPropositionStore();
        await store.WriteAsync(
            new PropositionBatch(
                [new StoredProposition("customer.a", "customer", Document, 1, null)],
                ["customer.gone"]),
            default);

        // Assert
        store.Load().ShouldHaveSingleItem();
        store.Load()[0].Name.ShouldBe("customer.a");
    }

    [Fact]
    public async Task Should_serialise_a_proposition_write_against_a_rule_write()
    {
        // Arrange — the two sets share one BindingScope, so they share one outer gate. A store that
        // records entry and exit is the only way to observe whether the two writes interleaved.
        var timeline = new List<string>();
        var scope = new BindingScope(new SpecRegistry().Register("customer.is-active", IsActive));

        var propositions = new PropositionSet(scope, new TracingPropositionStore(timeline))
            .AddModel<Customer>("customer");
        propositions.Load();

        var rules = new RuleSet(scope, new TracingRuleStore(timeline)).Add(new SampleRule());
        rules.Load();

        // Act — launched together, they must not interleave
        await Task.WhenAll(
            propositions.CreateAsync("customer.a", "customer", Document, null),
            rules.UpdateAsync("sample", Document, 1, new RuleChangeProvenance("alice")));

        // Assert — each store's enter/exit pair must be contiguous; an interleave would read
        // "proposition-enter, rule-enter, ...". This is what the outer gate buys that the inner
        // Monitor cannot: the Monitor is released at the first await.
        //
        // Joined to a string on purpose: Shouldly's ShouldBeOneOf compares with
        // EqualityComparer<T>.Default, which for List<string> is reference equality and can never
        // match a literal. Comparing the joined string also puts the real order in the failure message.
        string.Join(",", timeline).ShouldBeOneOf(
            "proposition-enter,proposition-exit,rule-enter,rule-exit",
            "rule-enter,rule-exit,proposition-enter,proposition-exit");
    }

    private sealed record Customer(bool IsActive);
}
