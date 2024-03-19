namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsQueenHighStraightSpec() : Spec<Hand>(
    Spec.Build(new DoesHandContainSpecifiedRanksSpec(QueenHighStraight))
        .WhenTrue("is Queen High Straight")
        .WhenFalse("is Not Queen High Straight")
        .Create())
{
    private static readonly ICollection<Rank> QueenHighStraight = [Rank.Queen, Rank.Jack, Rank.Ten, Rank.Nine, Rank.Eight];
}