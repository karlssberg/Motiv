using static Karlssberg.Motive.Poker.HandRank;

namespace Karlssberg.Motive.Poker.HandRanks;

public class IsHandTwoPairSpec() : Spec<Hand, HandRank>(
    "Is a Two Pair hand",
    new HasNPairsSpec(2),
    TwoPair,
    HighCard);