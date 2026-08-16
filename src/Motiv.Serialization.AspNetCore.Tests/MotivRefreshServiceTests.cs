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
    /// Waits for a condition using <see cref="MotivRefreshService.WaitForTickAsync"/> — the service's
    /// own per-tick completion signal — instead of polling against a wall-clock deadline. Each
    /// iteration awaits the next completed poll cycle (or, if one already completed before this was
    /// first called, the pending signal it left behind — see that method's doc for why that can never
    /// be lost) and rechecks <paramref name="condition"/>. This is what makes the wait itself
    /// deadline-free: progress here is driven entirely by the service actually ticking, not by how
    /// much wall-clock time a possibly-contended test runner has burned through.
    /// </summary>
    /// <remarks>
    /// A bounded backstop remains, so a genuine hang — the service never ticking at all — still goes
    /// red instead of hanging CI. A healthy run never approaches it: the loop only ever waits on real
    /// ticks, so its actual running time tracks how many ticks the condition needed, not the backstop.
    /// Returns whether the condition was actually observed true, rather than just falling through the
    /// backstop — a caller asserting on state read *after* this returns must know the wait itself
    /// succeeded.
    /// </remarks>
    private static async Task<bool> WaitForTickWhereAsync(MotivRefreshService service, Func<bool> condition)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        try
        {
            while (!condition())
                await service.WaitForTickAsync(cts.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
        }
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

        // Act — wait on b's own version, not on service.LastReport.Outcome. LastReport is momentary
        // by design: it holds only the most recently completed tick's report, and the poller keeps
        // ticking every 20ms, so RefreshOutcome.Applied is true for exactly the one tick that applied
        // it and is overwritten by the very next "Unchanged" tick. WaitForTickWhereAsync only rechecks
        // its condition between ticks, so a waiter that isn't scheduled during that single ~20ms
        // window misses the transient Applied value entirely and then observes nothing but Unchanged
        // until the backstop fires — no amount of tightening the wait helper closes that gap, because
        // the thing being waited on is inherently a single-tick pulse, not a state. This is not a
        // check-then-act race (an earlier version of this comment claimed the outcome-based wait
        // "removed" one); it is a durability problem — the awaited value simply stops being true.
        // b's Version, by contrast, is monotonic and permanent: once the replica has rebuilt onto
        // generation 2, it stays there forever, so there is no window in which a poll of it can revert
        // to false. Waiting on it is waiting on a durable fact, not a transient report of one.
        await service.StartAsync(default);
        bool converged;
        try
        {
            converged = await WaitForTickWhereAsync(service, () => b.FindEntry("number")!.Version == 2);
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
        // then pass without ever having exercised the failure it exists to cover. Waiting via the
        // service's own per-tick signal means each iteration corresponds to one real completed poll
        // cycle — success or caught failure — rather than a fixed wall-clock interval.
        await service.StartAsync(default);
        bool observedThreeFailures;
        try
        {
            observedThreeFailures = await WaitForTickWhereAsync(service, () => store.Failures >= 3);
        }
        finally
        {
            await service.StopAsync(default);
        }

        // Assert — the loop absorbed every failure. Taking the host down over an unreachable store
        // would trade a stale replica for no replica.
        observedThreeFailures.ShouldBeTrue();
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
