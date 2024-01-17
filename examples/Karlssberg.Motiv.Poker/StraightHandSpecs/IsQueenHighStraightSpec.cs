namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsQueenHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Queen, Rank.Jack, Rank.Ten, Rank.Nine, Rank.Eight])
        .YieldWhenTrue("is Queen High Straight")
        .YieldWhenFalse("is Not Queen High Straight")
        .CreateSpec());