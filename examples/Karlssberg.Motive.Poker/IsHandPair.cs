namespace Karlssberg.Motive.Poker;

public class IsHandPair() : Specification<Hand, HandRank>(
    new HasNPairs(1),
    HandRank.Pair,
    HandRank.HighCard);