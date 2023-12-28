namespace Karlssberg.Motive.Poker;

public class IsHandStraight() : Spec<Hand, HandRank>(
    new HasStraightCards(),
    HandRank.Straight,
    HandRank.HighCard);