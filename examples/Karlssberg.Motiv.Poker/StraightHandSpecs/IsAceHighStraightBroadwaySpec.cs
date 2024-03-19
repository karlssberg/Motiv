namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsAceHighStraightBroadwaySpec() : Spec<Hand>(
    Spec.Build(new DoesHandContainSpecifiedRanksSpec(BroadwayRanks))
        .WhenTrue("Is Ace High Straight Broadway")
        .WhenFalse("Is Not Ace High Straight Broadway")
        .Create())
{
    private static readonly ICollection<Rank> BroadwayRanks =
        [Rank.Ace, Rank.King, Rank.Queen, Rank.Jack, Rank.Ten];
};