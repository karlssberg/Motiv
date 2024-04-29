namespace Motiv.Poker.StraightHandProposition;

public class IsKingHighStraightProposition() : Spec<Hand>(
    Spec.Build(new DoAllCardsMatchRanksProposition(KingHighStraight))
        .WhenTrue("is King High Straight")
        .WhenFalse("is Not King High Straight")
        .Create())
{
    private static readonly ICollection<Rank> KingHighStraight = [Rank.King, Rank.Queen, Rank.Jack, Rank.Ten, Rank.Nine];
}