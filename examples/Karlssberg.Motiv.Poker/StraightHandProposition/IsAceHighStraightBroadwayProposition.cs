namespace Karlssberg.Motiv.Poker.StraightHandProposition;

public class IsAceHighStraightBroadwayProposition() : Spec<Hand>(
    Spec.Build(new DoesHandContainSpecifiedRanksProposition(BroadwayRanks))
        .WhenTrue("Is Ace High Straight Broadway")
        .WhenFalse("Is Not Ace High Straight Broadway")
        .Create())
{
    private static readonly ICollection<Rank> BroadwayRanks =
        [Rank.Ace, Rank.King, Rank.Queen, Rank.Jack, Rank.Ten];
};