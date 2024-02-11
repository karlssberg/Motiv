namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsFiveHighStraightWheelOrBicycleSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Five, Rank.Four, Rank.Three, Rank.Two, Rank.Ace])
        .WhenTrue("is Five High Straight Wheel Or Bicycle")
        .WhenFalse("is Not Five High Straight Wheel Or Bicycle"));