using Microsoft.EntityFrameworkCore;

namespace Motiv.Serialization.EntityFrameworkCore;

/// <summary>
/// The two <see cref="StoreGenerationRow"/> rows, and the only two things ever done to them: read
/// where a store stands, and move it on.
/// </summary>
/// <remarks>
/// Shared by <see cref="EfRuleStore"/> and <see cref="EfPropositionStore"/> so that neither has to
/// reach into the other for it. The scope argument is what keeps the two stores independent: they
/// address different rows, and are never written in the same transaction.
/// </remarks>
internal static class GenerationTracking
{
    /// <summary>The scope key of the rule store's row.</summary>
    public const string RulesScope = "rules";

    /// <summary>The scope key of the proposition store's row.</summary>
    public const string PropositionsScope = "propositions";

    /// <summary>
    /// Where the given scope stands. An absent row is generation zero, not an error — a store that
    /// has never been written to has never moved.
    /// </summary>
    public static async Task<long> ReadAsync(
        MotivStoreDbContext context, string scope, CancellationToken cancellationToken)
    {
        var row = await context.StoreGenerations.AsNoTracking()
            .SingleOrDefaultAsync(generation => generation.Scope == scope, cancellationToken);

        return row?.Generation ?? 0;
    }

    /// <summary>
    /// Moves the given scope's generation, tracked so the increment is written by the caller's
    /// <c>SaveChangesAsync</c> — the bump and the write it describes land in one transaction.
    /// </summary>
    public static async Task BumpAsync(
        MotivStoreDbContext context, string scope, CancellationToken cancellationToken)
    {
        var row = await context.StoreGenerations
            .SingleOrDefaultAsync(generation => generation.Scope == scope, cancellationToken);

        if (row is null)
            context.StoreGenerations.Add(new StoreGenerationRow { Scope = scope, Generation = 1 });
        else
            row.Generation++;
    }
}
