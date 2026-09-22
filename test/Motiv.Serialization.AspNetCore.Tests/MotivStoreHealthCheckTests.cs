using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Motiv.Serialization.AspNetCore.Tests;

/// <summary>
/// Readiness: can this replica's stores answer at all? Distinct from
/// <see cref="MotivRefreshHealthCheck"/>, which asks whether it has converged.
/// </summary>
public class MotivStoreHealthCheckTests
{
    private static SpecBase<int, string> Positive { get; } = Spec.Build((int n) => n > 0).Create("positive");

    private sealed class NumberRule() : Rule<int, string>("number", Positive);

    /// <summary>A store whose generation read can be made to fail or hang on demand.</summary>
    private sealed class FaultyRuleStore : IRuleStore
    {
        private readonly InMemoryRuleStore _inner = new();

        public Exception? GenerationFault { get; set; }

        public Task<long> GetGenerationAsync(CancellationToken cancellationToken) =>
            GenerationFault is { } fault
                ? Task.FromException<long>(fault)
                : _inner.GetGenerationAsync(cancellationToken);

        public IReadOnlyList<StoredRule> Load() => _inner.Load();

        public Task<IReadOnlyList<StoredRule>> LoadAsync(CancellationToken cancellationToken) =>
            _inner.LoadAsync(cancellationToken);

        public Task<RuleAppendResult> AppendAsync(
            IReadOnlyList<StoredRuleVersion> rows, CancellationToken cancellationToken) =>
            _inner.AppendAsync(rows, cancellationToken);

        public Task<IReadOnlyList<StoredRuleVersion>> HistoryAsync(
            string name, CancellationToken cancellationToken) =>
            _inner.HistoryAsync(name, cancellationToken);
    }

    private static RuleSet RulesOver(IRuleStore store)
    {
        var rules = new RuleSet(new SpecRegistry().Register("positive", Positive), store).Add(new NumberRule());
        rules.Load();
        return rules;
    }

    [Fact]
    public async Task Should_report_ready_when_the_store_answers()
    {
        // Arrange
        var check = new MotivStoreHealthCheck(RulesOver(new InMemoryRuleStore()), propositions: null);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), default);

        // Assert
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Data["rules.generation"].ShouldBe(0L);
    }

    [Fact]
    public async Task Should_report_unready_when_the_store_cannot_answer()
    {
        // Arrange — an unreachable database is exactly what readiness exists to catch. The fault is
        // armed after Load, so the replica starts healthy and the store fails underneath it, which is
        // the order this actually happens in.
        var store = new FaultyRuleStore();
        var check = new MotivStoreHealthCheck(RulesOver(store), propositions: null);
        store.GenerationFault = new InvalidOperationException("connection refused");

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), default);

        // Assert — unhealthy, not degraded: a replica whose store will not answer cannot publish and
        // cannot converge, so taking it out of rotation is the correct thing to do.
        result.Status.ShouldBe(HealthStatus.Unhealthy);
        result.Description!.ShouldContain("rule store");
        result.Exception!.Message.ShouldBe("connection refused");
    }

    [Fact]
    public async Task Should_report_both_halves_when_the_host_has_propositions()
    {
        // Arrange
        // Propositions first, then the rules paired to them — one scope, which is how a host builds
        // them and the only pairing SpecRegistry.ClaimScope allows.
        var registry = new SpecRegistry().Register("positive", Positive);
        var propositions = new PropositionSet(registry, new InMemoryPropositionStore());
        var rules = new RuleSet(propositions).Add(new NumberRule());

        var check = new MotivStoreHealthCheck(rules, propositions);

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), default);

        // Assert — two stores, never written in the same transaction, so both are probed.
        result.Status.ShouldBe(HealthStatus.Healthy);
        result.Data.ShouldContainKey("rules.generation");
        result.Data.ShouldContainKey("propositions.generation");
    }

    [Fact]
    public async Task Should_honour_the_probes_own_cancellation()
    {
        // Arrange — a health endpoint bounds its own probes; the check must pass that bound down
        // rather than block the endpoint on a store that never answers.
        var store = new FaultyRuleStore();
        var check = new MotivStoreHealthCheck(RulesOver(store), propositions: null);
        store.GenerationFault = new OperationCanceledException();
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        // Act
        var result = await check.CheckHealthAsync(new HealthCheckContext(), cancelled.Token);

        // Assert
        result.Status.ShouldBe(HealthStatus.Unhealthy);
    }

    [Fact]
    public async Task Should_be_registered_by_AddMotivRules_under_the_ready_tag()
    {
        // Arrange — an ordinary host, with nothing asked for beyond the rules themselves.
        await using var host = TestApp.Create();

        // Act
        var report = await host.Services
            .GetRequiredService<HealthCheckService>()
            .CheckHealthAsync(registration => registration.Tags.Contains("ready"), default);

        // Assert — a probe nobody remembered to enable is a replica that stays in rotation with an
        // unreachable database, so this is registered rather than offered.
        report.Status.ShouldBe(HealthStatus.Healthy);
        report.Entries.ShouldContainKey("motiv-store");
    }
}
