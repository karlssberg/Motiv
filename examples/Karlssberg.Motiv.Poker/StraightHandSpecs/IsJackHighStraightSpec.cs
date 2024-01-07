namespace Karlssberg.Motiv.Poker.StraightHands;

public class IsJackHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Jack, Rank.Ten, Rank.Nine, Rank.Eight, Rank.Seven])
        .YieldWhenTrue("is Jack High Straight")
        .YieldWhenFalse("is Not Jack High Straight")
        .CreateSpec());