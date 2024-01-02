namespace Karlssberg.Motiv.Poker.StraightHands;

public class IsFiveHighStraightWheelOrBicycleSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Five, Rank.Four, Rank.Three, Rank.Two, Rank.Ace]),
    "Is Five High Straight Wheel Or Bicycle",
    "Is Not Five High Straight Wheel Or Bicycle");