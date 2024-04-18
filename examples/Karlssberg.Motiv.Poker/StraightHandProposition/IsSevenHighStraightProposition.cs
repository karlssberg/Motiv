namespace Karlssberg.Motiv.Poker.StraightHandProposition;

public class IsSevenHighStraightProposition() : Spec<Hand>(
    Spec.Build(new DoAllCardsMatchRanksProposition(SevenHighStraight))
        .WhenTrue("is Seven High Straight")
        .WhenFalse("is Not Seven High Straight")
        .Create())
{
    private static readonly ICollection<Rank> SevenHighStraight = [Rank.Seven, Rank.Six, Rank.Five, Rank.Four, Rank.Three];
}