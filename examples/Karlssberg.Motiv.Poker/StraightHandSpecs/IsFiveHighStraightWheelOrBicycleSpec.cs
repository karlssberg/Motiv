namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsFiveHighStraightWheelOrBicycleSpec() : Spec<Hand>(
    Spec.Build(new DoesHandContainSpecifiedRanksSpec(FiveHighStraight))
        .WhenTrue("is Five High Straight Wheel Or Bicycle")
        .WhenFalse("is Not Five High Straight Wheel Or Bicycle")
        .CreateSpec())
{
    private static readonly ICollection<Rank> FiveHighStraight =
        [Rank.Five, Rank.Four, Rank.Three, Rank.Two, Rank.Ace];
}