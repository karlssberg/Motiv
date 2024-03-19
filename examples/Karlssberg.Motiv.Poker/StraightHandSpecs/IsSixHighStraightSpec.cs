namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsSixHighStraightSpec() : Spec<Hand>(
    Spec.Build(new DoesHandContainSpecifiedRanksSpec(SixHighStraight))
        .WhenTrue("is Six High Straight")
        .WhenFalse("is Not Six High Straight")
        .Create())
{
    private static readonly ICollection<Rank> SixHighStraight = [Rank.Six, Rank.Five, Rank.Four, Rank.Three, Rank.Two];
}