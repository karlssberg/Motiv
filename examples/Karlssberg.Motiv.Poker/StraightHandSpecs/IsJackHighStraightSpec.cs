namespace Karlssberg.Motiv.Poker.StraightHands;

public class IsJackHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Jack, Rank.Ten, Rank.Nine, Rank.Eight, Rank.Seven]),
    "Is Jack High Straight",
    "Is Not Jack High Straight");