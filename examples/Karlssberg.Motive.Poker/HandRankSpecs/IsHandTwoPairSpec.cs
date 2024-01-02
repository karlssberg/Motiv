using static Karlssberg.Motive.Poker.HandRank;

namespace Karlssberg.Motive.Poker.HandRankSpecs;

public class IsHandTwoPairSpec() : Spec<Hand, HandRank>(
    "Is a Two Pair hand",
    new HasNPairsSpec(2),
    TwoPair,
    HighCard);