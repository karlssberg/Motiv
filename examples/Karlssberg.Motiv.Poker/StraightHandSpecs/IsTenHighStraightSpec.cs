namespace Karlssberg.Motiv.Poker.StraightHands;

public class IsTenHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Ten, Rank.Nine, Rank.Eight, Rank.Seven, Rank.Six]),
    "Is Ten High Straight",
    "Is Not Ten High Straight");