namespace Karlssberg.Motive.Poker.StraightHands;

public class IsKingHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.King, Rank.Queen, Rank.Jack, Rank.Ten, Rank.Nine]),
    "Is King High Straight",
    "Is Not King High Straight");