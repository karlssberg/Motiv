using static Motiv.Tests.MemoIdentity.MemoIdentityPropositions;

namespace Motiv.Tests.MemoIdentity;

/// <summary>
/// An explanation and a metadata tier resolve the level beneath them once: their <c>Branches</c>, which the
/// root-assertion and root-values walks descend, are the same list on every read rather than a fresh
/// resolution of their causes (#294).
/// <para>
/// A performance guard, like every test in this folder: a rebuild is an equal value, so these pin the cost
/// of a re-read, not its output (see <see cref="MemoisedResultCase" />).
/// </para>
/// </summary>
public class BranchMemoIdentityTests
{
    private static BooleanResultBase<string> Composition() =>
        Proposition("left").And(Proposition("right")).Evaluate(true);

    [Fact]
    public void An_explanation_resolves_its_branches_once()
    {
        var explanation = Composition().Explanation;

        explanation.Branches.ShouldNotBeEmpty();
        explanation.Branches.ShouldBeSameAs(explanation.Branches);
    }

    [Fact]
    public void A_metadata_tier_resolves_its_branches_once()
    {
        var tier = Composition().MetadataTier;

        tier.Branches.ShouldNotBeEmpty();
        tier.Branches.ShouldBeSameAs(tier.Branches);
    }
}
