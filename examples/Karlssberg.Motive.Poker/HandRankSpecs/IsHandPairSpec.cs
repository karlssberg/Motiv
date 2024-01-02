namespace Karlssberg.Motive.Poker.HandRankSpecs;

public class IsHandPairSpec() : Spec<Hand, HandRank>(
    "Is a Pair hand",
    new HasNPairsSpec(1),
    HandRank.Pair,
    HandRank.HighCard);