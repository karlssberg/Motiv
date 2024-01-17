namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsTenHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Ten, Rank.Nine, Rank.Eight, Rank.Seven, Rank.Six])
        .YieldWhenTrue("is Ten High Straight")
        .YieldWhenFalse("is Not Ten High Straight")
        .CreateSpec());