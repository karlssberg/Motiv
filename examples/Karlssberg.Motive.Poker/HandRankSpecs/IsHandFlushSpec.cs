namespace Karlssberg.Motive.Poker.HandRankSpecs;

public class IsHandFlushSpec() : Spec<Hand, HandRank>(
    "Is a Flush hand",
    new HasNCardsWithTheSameSuitSpec(5),
    HandRank.Flush,
    HandRank.HighCard);