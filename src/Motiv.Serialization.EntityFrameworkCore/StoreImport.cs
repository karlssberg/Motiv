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
/// <para>
/// That refusal is also why a failure part-way through is thrown rather than reported: the two
/// stores are never written in the same transaction, so an import cannot be atomic across them, and
/// a target left holding some of the import would refuse every later run — reporting
/// <c>Imported: false</c>, indistinguishable from the benign already-done case. See
/// <see cref="CopyAsync"/> for what is atomic and what is not.
/// </para>
/// </remarks>
public static class StoreImport
{
    /// <summary>
    /// Copies the propositions and then every rule's version log into a target that must be empty.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <strong>Not atomic across the two stores, and not atomic across rule names.</strong> Nothing
    /// could make it so: the rule store and the proposition store are never written in the same
    /// transaction, and the rule side is one <see cref="IRuleStore.AppendAsync"/> call per name. What
    /// is guaranteed is narrower and worth stating exactly — each rule name arrives with its whole
    /// version log or not at all, and the propositions arrive as one batch or not at all.
    /// </para>
    /// <para>
    /// Propositions are copied first, so the rule store — the side <see cref="CopyAsync"/> checks
    /// first when deciding whether the target is empty — is the last thing to become non-empty. Any
    /// failure after the first write throws <see cref="InvalidOperationException"/> naming the
    /// partial state, because a partially imported target is not something a retry can fix: it must
    /// be emptied first.
    /// </para>
    /// </remarks>
    /// <param name="sourceRules">The rule store to read from.</param>
    /// <param name="targetRules">The rule store to write to. Must be empty.</param>
    /// <param name="sourcePropositions">The proposition store to read from.</param>
    /// <param name="targetPropositions">The proposition store to write to. Must be empty.</param>
    /// <param name="cancellationToken">Cancels the copy. Mid-copy, this is a partial import.</param>
    /// <exception cref="InvalidOperationException">
    /// The copy failed after writing something. The target is partially imported and must be
    /// emptied before the import is run again.
    /// </exception>
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

        // Everything fallible that can be done before anything mutates, is: both sources are read
        // out in full — every name's whole history included — so a source that cannot be read fails
        // while the target is still empty and a retry is still clean.
        var propositions = await sourcePropositions.LoadAsync(cancellationToken);

        var histories = new List<IReadOnlyList<StoredRuleVersion>>();
        foreach (var head in await sourceRules.LoadAsync(cancellationToken))
        {
            var history = await sourceRules.HistoryAsync(head.Name, cancellationToken);
            if (history.Count > 0)
                histories.Add(history);
        }

        var ruleVersions = 0;
        var propositionsWritten = 0;
        try
        {
            if (propositions.Count > 0)
            {
                await targetPropositions.WriteAsync(
                    new PropositionBatch(propositions, []), cancellationToken);
                propositionsWritten = propositions.Count;
            }

            foreach (var history in histories)
            {
                // One append per name, carrying that name's whole log: the batch is all-or-nothing,
                // so a name either arrives complete or not at all.
                var result = await targetRules.AppendAsync(history, cancellationToken);
                if (result.IsConflict)
                {
                    throw new InvalidOperationException(
                        $"Import of rule '{result.Name}' conflicted at version {result.CurrentVersion}. " +
                        "The target was empty when the import began, so something else is writing to it.");
                }

                ruleVersions += history.Count;
            }
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
                                          && (propositionsWritten > 0 || ruleVersions > 0))
        {
            throw new InvalidOperationException(
                $"The import failed after copying {propositionsWritten} proposition(s) and " +
                $"{ruleVersions} rule version row(s). The target is now PARTIALLY imported: running " +
                "the import again will not repair it, because a non-empty target is refused and " +
                "reported as nothing-to-import. Empty the target — drop and recreate its tables — " +
                $"and run the import again. The failure was: {exception.Message}",
                exception);
        }

        return new StoreImportResult(true, ruleVersions, propositions.Count);
    }
}
