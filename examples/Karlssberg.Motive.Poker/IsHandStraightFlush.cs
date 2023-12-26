namespace Karlssberg.Motive.Poker;

public class IsHandStraightFlush() : Specification<Hand, HandRank>(
    new HasStraightCards() & new IsMaxCardsWithTheSameSuit(5),
    HandRank.StraightFlush,
    HandRank.HighCard);