namespace Motiv.Serialization;

/// <summary>What a refresh did.</summary>
public enum RefreshOutcome
{
    /// <summary>Neither store had moved, so nothing was rebuilt. The common case, every tick.</summary>
    Unchanged,

    /// <summary>A new world was built and swapped in.</summary>
    Applied,

    /// <summary>
    /// The rebuild would have regressed a live binding to its compiled default, so it was discarded
    /// and the current world kept.
    /// </summary>
    Aborted,

    /// <summary>A publish landed while the rebuild was being built, so it was discarded. Retry.</summary>
    Contended
}

/// <summary>One node a refresh could not bind, and why.</summary>
/// <param name="Name">The rule or proposition name.</param>
/// <param name="Kind">"rule" or "proposition", matching <c>NodeId.KindLabel</c>.</param>
/// <param name="Errors">Why it would not bind.</param>
public sealed record RefreshFailure(string Name, string Kind, IReadOnlyList<RuleError> Errors);

/// <summary>
/// The outcome of a <see cref="RuleSet.RefreshAsync"/>: what happened, where the stores stood, and
/// anything that would not bind.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Regressions"/> and <see cref="Quarantined"/> are different in kind, not in degree. A
/// regression is a document that binds in the world being served and would not bind in the world
/// being built — applying it would drop a live, approved rule back to compiled behaviour nobody
/// approved, so the refresh refuses. Something already quarantined has no live binding to protect, so
/// it is carried forward and reported without blocking convergence.
/// </para>
/// <para>
/// <strong>The distinction is what stops a refresh being useless.</strong> The obvious rule — abort
/// whenever anything fails to bind — reads as the safe one and is not: a single hand-edited row that
/// never bound in the first place would abort every refresh from then on, and the replica would
/// converge on <em>nothing</em>, forever, while the operator watched an alert that named a row they
/// had already given up on. Quarantine exists precisely so that a bad row costs its own row; carrying
/// one across a refresh is that same promise applied to time rather than to the catalog.
/// </para>
/// <para>
/// Both lists are reported either way, because an operator needs to see the second kind even though
/// it changed nothing: a row that is carried today is a row that will be carried every tick until
/// someone repairs it, and silence about it would be indistinguishable from health.
/// </para>
/// </remarks>
public sealed class RefreshReport
{
    private RefreshReport(
        RefreshOutcome outcome, StoreGeneration generation,
        IReadOnlyList<RefreshFailure> regressions, IReadOnlyList<RefreshFailure> quarantined)
    {
        Outcome = outcome;
        Generation = generation;
        Regressions = regressions;
        Quarantined = quarantined;
    }

    /// <summary>What happened.</summary>
    public RefreshOutcome Outcome { get; }

    /// <summary>Where both stores stood in the world now being served.</summary>
    public StoreGeneration Generation { get; }

    /// <summary>What would have regressed, and therefore why an <see cref="RefreshOutcome.Aborted"/> refresh aborted.</summary>
    public IReadOnlyList<RefreshFailure> Regressions { get; }

    /// <summary>What was carried forward still quarantined. Never blocks a refresh.</summary>
    public IReadOnlyList<RefreshFailure> Quarantined { get; }

    /// <summary>Whether this replica converged, or is knowingly serving an older world.</summary>
    /// <remarks>
    /// The one line a health check wants. <see cref="RefreshOutcome.Contended"/> is <em>not</em>
    /// converged even though nothing is wrong with it — the replica is behind at the moment it is
    /// asked, and a caller polling on a timer should retry rather than report success.
    /// </remarks>
    public bool IsConverged => Outcome is RefreshOutcome.Applied or RefreshOutcome.Unchanged;

    /// <summary>Neither store had moved.</summary>
    public static RefreshReport Unchanged(StoreGeneration generation) =>
        new(RefreshOutcome.Unchanged, generation, [], []);

    /// <summary>A new world went live, carrying <paramref name="quarantined"/> forward still broken.</summary>
    public static RefreshReport Applied(StoreGeneration generation, IReadOnlyList<RefreshFailure> quarantined) =>
        new(RefreshOutcome.Applied, generation, [], quarantined);

    /// <summary>The rebuild was discarded because <paramref name="regressions"/> would have lost a live binding.</summary>
    public static RefreshReport Aborted(StoreGeneration generation, IReadOnlyList<RefreshFailure> regressions) =>
        new(RefreshOutcome.Aborted, generation, regressions, []);

    /// <summary>A publish beat the rebuild to the swap.</summary>
    public static RefreshReport Contended(StoreGeneration generation) =>
        new(RefreshOutcome.Contended, generation, [], []);
}
