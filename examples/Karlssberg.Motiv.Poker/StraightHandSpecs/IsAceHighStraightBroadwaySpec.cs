namespace Karlssberg.Motiv.Poker.StraightHands;

public class IsAceHighStraightBroadwaySpec() : Spec<Hand>(
    new DoesHandContainSpecifiedRanksSpec([Rank.Ace, Rank.King, Rank.Queen, Rank.Jack, Rank.Ten]),
    "Is Ace High Straight Broadway",
    "Is Not Ace High Straight Broadway");