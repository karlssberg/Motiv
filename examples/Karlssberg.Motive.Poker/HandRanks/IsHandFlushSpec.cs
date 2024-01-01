namespace Karlssberg.Motive.Poker.HandRanks;

public class IsHandFlushSpec() : Spec<Hand, HandRank>(
    "Is a Flush hand",
    hand => hand.Suits.Distinct().Count() == 1,
    HandRank.Flush,
    HandRank.HighCard);