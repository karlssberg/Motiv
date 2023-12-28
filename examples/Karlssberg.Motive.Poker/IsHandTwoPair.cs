using static Karlssberg.Motive.Poker.HandRank;

namespace Karlssberg.Motive.Poker;

public class IsHandTwoPair() : Spec<Hand, HandRank>(
    new HasNPairs(2),
    TwoPair,
    HighCard);