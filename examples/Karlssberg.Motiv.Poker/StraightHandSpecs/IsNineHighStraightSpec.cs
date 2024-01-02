namespace Karlssberg.Motiv.Poker.StraightHands;

public class IsNineHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Nine, Rank.Eight, Rank.Seven, Rank.Six, Rank.Five]),
    "Is Nine High Straight",
    "Is Not Nine High Straight");