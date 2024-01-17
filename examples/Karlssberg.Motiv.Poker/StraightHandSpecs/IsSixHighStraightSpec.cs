namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsSixHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Six, Rank.Five, Rank.Four, Rank.Three, Rank.Two])
        .YieldWhenTrue("is Six High Straight")
        .YieldWhenFalse("is Not Six High Straight")
        .CreateSpec());