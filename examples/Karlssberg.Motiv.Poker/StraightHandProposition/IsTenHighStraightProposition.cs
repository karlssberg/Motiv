namespace Karlssberg.Motiv.Poker.StraightHandProposition;

public class IsTenHighStraightProposition() : Spec<Hand>(
    Spec.Build(new DoAllCardsMatchRanksProposition(TenHighStraight))
        .WhenTrue("is Ten High Straight")
        .WhenFalse("is Not Ten High Straight")
        .Create())
{
    private static readonly ICollection<Rank> TenHighStraight = [Rank.Ten, Rank.Nine, Rank.Eight, Rank.Seven, Rank.Six];
}