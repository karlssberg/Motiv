using Motiv.Serialization;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>What a <see cref="StoreImport.CopyAsync"/> did.</summary>
/// <param name="Imported">
/// False when a target already held data and nothing was copied. Not an error: it is what makes
/// running the import on every startup harmless.
/// </param>
/// <param name="RuleVersions">How many version rows were replayed.</param>
/// <param name="Propositions">How many proposition rows were copied.</param>
public sealed record StoreImportResult(bool Imported, int RuleVersions, int Propositions);

/// <summary>
/// A one-way copy from one pair of stores into another — the migration path off the file-backed
/// stores and onto a database.
/// </summary>
/// <remarks>
/// <para>
/// Deliberately written against <see cref="IRuleStore"/> and <see cref="IPropositionStore"/> alone,
/// with no knowledge of either end. The rule side replays the <em>whole</em> version log rather than
/// the head, because a head-only copy would restamp every rule as authored at import time and
/// destroy the audit trail an approval gate depends on.
/// </para>
/// <para>
/// Refuses a non-empty target rather than throwing or merging. A merge could not preserve version
/// numbers, and refusing means a second run is a no-op — so no import state has to be recorded
/// anywhere to keep this idempotent.
/// </para>
/// </remarks>
public static class StoreImport
{
    /// <summary>Copies both stores, or neither.</summary>
    public static async Task<StoreImportResult> CopyAsync(
        IRuleStore sourceRules,
        IRuleStore targetRules,
        IPropositionStore sourcePropositions,
        IPropositionStore targetPropositions,
        CancellationToken cancellationToken)
    {
        var existingRules = await targetRules.LoadAsync(cancellationToken);
        var existingPropositions = await targetPropositions.LoadAsync(cancellationToken);

        if (existingRules.Count > 0 || existingPropositions.Count > 0)
            return new StoreImportResult(false, 0, 0);

        var ruleVersions = 0;
        foreach (var head in await sourceRules.LoadAsync(cancellationToken))
        {
            var history = await sourceRules.HistoryAsync(head.Name, cancellationToken);
            if (history.Count == 0)
                continue;

            // One append per name, carrying that name's whole log: the batch is all-or-nothing, so
            // a name either arrives complete or not at all.
            var result = await targetRules.AppendAsync(history, cancellationToken);
            if (result.IsConflict)
            {
                throw new InvalidOperationException(
                    $"Import of rule '{result.Name}' conflicted at version {result.CurrentVersion}. " +
                    "The target was empty when the import began, so something else is writing to it.");
            }

            ruleVersions += history.Count;
        }

        var propositions = await sourcePropositions.LoadAsync(cancellationToken);
        if (propositions.Count > 0)
            await targetPropositions.WriteAsync(new PropositionBatch(propositions, []), cancellationToken);

        return new StoreImportResult(true, ruleVersions, propositions.Count);
    }
}
