namespace Karlssberg.Motiv.Poker.StraightHands;

public class IsNineHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Nine, Rank.Eight, Rank.Seven, Rank.Six, Rank.Five])
        .YieldWhenTrue("is Nine High Straight")
        .YieldWhenFalse("is Not Nine High Straight")
        .CreateSpec());