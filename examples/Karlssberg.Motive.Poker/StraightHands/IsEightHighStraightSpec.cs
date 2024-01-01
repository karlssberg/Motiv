namespace Karlssberg.Motive.Poker.StraightHands;

public class IsEightHighStraightSpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Eight, Rank.Seven, Rank.Six, Rank.Five, Rank.Four]),
    "Is Eight High Straight",
    "Is Not Eight High Straight");