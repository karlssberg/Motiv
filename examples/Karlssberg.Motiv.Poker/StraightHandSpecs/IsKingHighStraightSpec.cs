namespace Karlssberg.Motiv.Poker.StraightHands;

public class IsKingHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.King, Rank.Queen, Rank.Jack, Rank.Ten, Rank.Nine])
        .YieldWhenTrue("is King High Straight")
        .YieldWhenFalse("is Not King High Straight")
        .CreateSpec());