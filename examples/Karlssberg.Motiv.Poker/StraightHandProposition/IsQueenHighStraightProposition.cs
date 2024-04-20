namespace Karlssberg.Motiv.Poker.StraightHandProposition;

public class IsQueenHighStraightProposition() : Spec<Hand>(
    Spec.Build(new DoAllCardsMatchRanksProposition(QueenHighStraight))
        .WhenTrue("is Queen High Straight")
        .WhenFalse("is Not Queen High Straight")
        .Create())
{
    private static readonly ICollection<Rank> QueenHighStraight = [Rank.Queen, Rank.Jack, Rank.Ten, Rank.Nine, Rank.Eight];
}