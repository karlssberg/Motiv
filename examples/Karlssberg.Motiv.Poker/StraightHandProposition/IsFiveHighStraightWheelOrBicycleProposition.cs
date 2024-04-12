namespace Karlssberg.Motiv.Poker.StraightHandProposition;

public class IsFiveHighStraightWheelOrBicycleProposition() : Spec<Hand>(
    Spec.Build(new DoesHandContainSpecifiedRanksProposition(FiveHighStraight))
        .WhenTrue("is Five High Straight Wheel Or Bicycle")
        .WhenFalse("is Not Five High Straight Wheel Or Bicycle")
        .Create())
{
    private static readonly ICollection<Rank> FiveHighStraight =
        [Rank.Five, Rank.Four, Rank.Three, Rank.Two, Rank.Ace];
}