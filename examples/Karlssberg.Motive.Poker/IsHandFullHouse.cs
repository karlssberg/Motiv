namespace Karlssberg.Motive.Poker;

public class IsHandFullHouse() : Spec<Hand, HandRank>(
    new IsMaxCardsWithTheSameRank(2) & new IsMaxCardsWithTheSameRank(3),
    HandRank.FullHouse,
    HandRank.HighCard);