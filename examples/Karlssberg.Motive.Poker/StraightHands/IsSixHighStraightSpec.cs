namespace Karlssberg.Motive.Poker.StraightHands;

public class IsSixHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Six, Rank.Five, Rank.Four, Rank.Three, Rank.Two]),
    "Is Six High Straight",
    "Is Not Six High Straight");