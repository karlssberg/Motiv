namespace Karlssberg.Motive.Poker;

public class IsHandStraight() : Specification<Hand, HandRank>(
    new HasStraightCards(),
    HandRank.Straight,
    HandRank.HighCard);