namespace Motiv.Poker.StraightHandProposition;

public class IsJackHighStraightProposition() : Spec<Hand>(
    Spec.Build(new DoAllCardsMatchRanksProposition(JackHighStraight))
        .WhenTrue("is Jack High Straight")
        .WhenFalse("is Not Jack High Straight")
        .Create())
{
    private static readonly ICollection<Rank> JackHighStraight = 
        [Rank.Jack, Rank.Ten, Rank.Nine, Rank.Eight, Rank.Seven];
}