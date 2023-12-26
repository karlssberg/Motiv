using static Karlssberg.Motive.Poker.HandRank;

namespace Karlssberg.Motive.Poker;

public class IsHandTwoPair() : Specification<Hand, HandRank>(
    new HasNPairs(2),
    TwoPair,
    HighCard);