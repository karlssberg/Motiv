namespace Karlssberg.Motive.Poker;

public class IsHandFullHouse() : Specification<Hand, HandRank>(
    new IsMaxCardsWithTheSameRank(2) & new IsMaxCardsWithTheSameRank(3),
    HandRank.FullHouse,
    HandRank.HighCard);