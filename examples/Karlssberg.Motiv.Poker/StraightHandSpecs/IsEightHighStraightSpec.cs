namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsEightHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Eight, Rank.Seven, Rank.Six, Rank.Five, Rank.Four])
        .YieldWhenTrue("is Eight High Straight")
        .YieldWhenFalse("is Not Eight High Straight")
        .CreateSpec());