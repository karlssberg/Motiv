namespace Karlssberg.Motive.Poker;

public class IsHandFlush() : Specification<Hand, HandRank>(
    new IsMaxCardsWithTheSameSuit(5),
    HandRank.Flush,
    HandRank.HighCard);