using Microsoft.Extensions.Logging.Abstractions;

namespace Motiv.Serialization.AspNetCore.Tests;

public class MotivRefreshServiceTests
{
    private sealed class NumberRule() : Rule<int, string>("number", Spec.Build((int n) => n > 0).Create("positive"));

    private static RuleSet Replica(IRuleStore store)
    {
        var registry = new SpecRegistry().Register("positive", Spec.Build((int n) => n > 0).Create("positive"));
        var rules = new RuleSet(registry, store);
        rules.Add(new NumberRule());
        rules.Load();
        return rules;
    }

    /// <summary>
    /// Waits for the poller to get somewhere, rather than sleeping a fixed time: a fixed sleep is
    /// either flaky or slow. Bounded, so a hang goes red rather than hanging CI.
    /// </summary>
    private static async Task WaitUntil(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!condition() && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(10);
    }

    [Fact]
    public async Task Should_converge_a_replica_without_anyone_calling_refresh()
    {
        // Arrange
        var store = new InMemoryRuleStore();
        var a = Replica(store);
        var b = Replica(store);
        var service = new MotivRefreshService(
            b, new MotivRefreshOptions { Interval = TimeSpan.FromMilliseconds(20) },
            NullLogger<MotivRefreshService>.Instance);

        await a.UpdateAsync("number", """{"rule":{"not":{"spec":"positive"}}}""", 1, new RuleChangeProvenance("alice"));

        // Act
        await service.StartAsync(default);
        try
        {
            await WaitUntil(() => b.FindEntry("number")!.Version != 1);
        }
        finally
        {
            await service.StopAsync(default);
        }

        // Assert
        b.FindEntry("number")!.Version.ShouldBe(2);
        service.LastReport!.Outcome.ShouldBe(RefreshOutcome.Applied);
    }

    [Fact]
    public async Task Should_survive_a_store_that_throws_and_keep_polling()
    {
        // Arrange — the store is fine at startup and fails afterwards, which is the realistic
        // outage: the database was reachable when the pod booted and is not any more
        var store = new FailAfterStartupRuleStore();
        var rules = Replica(store);
        store.Failing = true;

        var service = new MotivRefreshService(
            rules, new MotivRefreshOptions { Interval = TimeSpan.FromMilliseconds(10) },
            NullLogger<MotivRefreshService>.Instance);

        // Act — wait for several ticks to have actually failed, rather than sleeping a fixed time
        // and hoping: on a slow machine a fixed sleep can span no ticks at all, and the test would
        // then pass without ever having exercised the failure it exists to cover.
        await service.StartAsync(default);
        try
        {
            await WaitUntil(() => store.Failures >= 3);
        }
        finally
        {
            await service.StopAsync(default);
        }

        // Assert — the loop absorbed every failure. Taking the host down over an unreachable store
        // would trade a stale replica for no replica.
        store.Failures.ShouldBeGreaterThanOrEqualTo(3);
        service.ExecuteTask!.IsFaulted.ShouldBeFalse();
        rules.FindEntry("number")!.Version.ShouldBe(1);
    }

    private sealed class FailAfterStartupRuleStore : IRuleStore
    {
        private readonly InMemoryRuleStore _inner = new();
        private int _failures;

        public bool Failing { get; set; }

        /// <summary>How many calls have been refused, so a test can wait for real ticks to have run.</summary>
        public int Failures => Volatile.Read(ref _failures);

        public IReadOnlyList<StoredRule> Load() => _inner.Load();

        public Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken ct) =>
            Failing ? throw Down() : _inner.LoadAsync(ct);

        public Task<long> GetGenerationAsync(CancellationToken ct) =>
            Failing ? throw Down() : _inner.GetGenerationAsync(ct);

        public Task<RuleAppendResult> AppendAsync(IReadOnlyList<StoredRuleVersion> versions, CancellationToken ct) =>
            Failing ? throw Down() : _inner.AppendAsync(versions, ct);

        public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(string name, CancellationToken ct) =>
            Failing ? throw Down() : _inner.HistoryAsync(name, ct);

        private Exception Down()
        {
            Interlocked.Increment(ref _failures);
            return new InvalidOperationException("store down");
        }
    }
}
