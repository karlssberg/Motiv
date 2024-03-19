namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsEightHighStraightSpec() : Spec<Hand>(
    Spec.Build(new DoesHandContainSpecifiedRanksSpec(EightHighStraight))
        .WhenTrue("is Eight High Straight")
        .WhenFalse("is Not Eight High Straight")
        .Create())
{
    private static readonly ICollection<Rank> EightHighStraight =
        [Rank.Eight, Rank.Seven, Rank.Six, Rank.Five, Rank.Four];
}