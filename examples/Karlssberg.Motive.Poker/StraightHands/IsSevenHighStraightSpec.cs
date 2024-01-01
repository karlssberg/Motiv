namespace Karlssberg.Motive.Poker.StraightHands;

public class IsSevenHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Seven, Rank.Six, Rank.Five, Rank.Four, Rank.Three]),
    "Is Seven High Straight",
    "Is Not Seven High Straight");