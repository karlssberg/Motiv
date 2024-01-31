namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsFiveHighStraightWheelOrBicycleSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Five, Rank.Four, Rank.Three, Rank.Two, Rank.Ace])
        .YieldWhenTrue("is Five High Straight Wheel Or Bicycle")
        .YieldWhenFalse("is Not Five High Straight Wheel Or Bicycle"));