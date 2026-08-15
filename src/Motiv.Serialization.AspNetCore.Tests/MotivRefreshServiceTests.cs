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
    /// either flaky or slow. Bounded, so a hang goes red rather than hanging CI. Returns whether the
    /// condition was actually observed true, rather than just falling through the deadline — a caller
    /// asserting on state read *after* this returns must know the wait itself succeeded, or a slow
    /// machine turns a timeout into a confusing failure somewhere else entirely.
    /// </summary>
    private static async Task<bool> WaitUntil(Func<bool> condition)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(5);
        while (!condition() && DateTimeOffset.UtcNow < deadline)
            await Task.Delay(10);
        return condition();
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

        // Act — wait on the outcome itself, not on the version and then a separate read of
        // LastReport: the version moving and LastReport being set are two different writes from the
        // same tick, and a poller that keeps ticking on a 20ms interval can overwrite LastReport with
        // a later "Unchanged" tick's report between this test observing the version change and it
        // reading LastReport, making that read flaky by construction rather than by environment.
        // Waiting on the outcome directly removes the check-then-act gap.
        await service.StartAsync(default);
        bool converged;
        try
        {
            converged = await WaitUntil(() => service.LastReport?.Outcome == RefreshOutcome.Applied);
        }
        finally
        {
            await service.StopAsync(default);
        }

        // Assert
        converged.ShouldBeTrue();
        b.FindEntry("number")!.Version.ShouldBe(2);
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
