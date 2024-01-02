using static Karlssberg.Motiv.Poker.HandRank;

namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandTwoPairSpec() : Spec<Hand, HandRank>(
    "Is a Two Pair hand",
    new HasNPairsSpec(2),
    TwoPair,
    HighCard);