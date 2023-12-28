namespace Karlssberg.Motive.Poker;

public class IsHandPair() : Spec<Hand, HandRank>(
    new HasNPairs(1),
    HandRank.Pair,
    HandRank.HighCard);