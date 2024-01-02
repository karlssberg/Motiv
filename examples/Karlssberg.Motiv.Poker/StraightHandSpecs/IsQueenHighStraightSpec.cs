namespace Karlssberg.Motiv.Poker.StraightHands;

public class IsQueenHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Queen, Rank.Jack, Rank.Ten, Rank.Nine, Rank.Eight]),
    "Is Queen High Straight",
    "Is Not Queen High Straight");