namespace Karlssberg.Motiv.Poker.StraightHandProposition;

public class IsEightHighStraightProposition() : Spec<Hand>(
    Spec.Build(new DoAllCardsMatchRanksProposition(EightHighStraight))
        .WhenTrue("is Eight High Straight")
        .WhenFalse("is Not Eight High Straight")
        .Create())
{
    private static readonly ICollection<Rank> EightHighStraight =
        [Rank.Eight, Rank.Seven, Rank.Six, Rank.Five, Rank.Four];
}