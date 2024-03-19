namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsSevenHighStraightSpec() : Spec<Hand>(
    Spec.Build(new DoesHandContainSpecifiedRanksSpec(SevenHighStraight))
        .WhenTrue("is Seven High Straight")
        .WhenFalse("is Not Seven High Straight")
        .Create())
{
    private static readonly ICollection<Rank> SevenHighStraight = [Rank.Seven, Rank.Six, Rank.Five, Rank.Four, Rank.Three];
}