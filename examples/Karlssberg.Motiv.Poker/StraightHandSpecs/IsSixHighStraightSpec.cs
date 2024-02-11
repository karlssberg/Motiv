namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsSixHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Six, Rank.Five, Rank.Four, Rank.Three, Rank.Two])
        .WhenTrue("is Six High Straight")
        .WhenFalse("is Not Six High Straight"));