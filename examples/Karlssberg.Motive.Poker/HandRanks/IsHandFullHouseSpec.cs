namespace Karlssberg.Motive.Poker.HandRanks;

public class IsHandFullHouseSpec() : Spec<Hand, HandRank>(
    "Is a Full House hand",
    new HasNCardsWithTheSameRankSpec(2) & new HasNCardsWithTheSameRankSpec(3),
    HandRank.FullHouse,
    HandRank.HighCard);