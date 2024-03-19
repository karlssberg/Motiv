namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsKingHighStraightSpec() : Spec<Hand>(
    Spec.Build(new DoesHandContainSpecifiedRanksSpec(KingHighStraight))
        .WhenTrue("is King High Straight")
        .WhenFalse("is Not King High Straight")
        .Create())
{
    private static readonly ICollection<Rank> KingHighStraight = [Rank.King, Rank.Queen, Rank.Jack, Rank.Ten, Rank.Nine];
}