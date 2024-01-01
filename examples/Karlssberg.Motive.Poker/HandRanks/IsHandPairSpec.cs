namespace Karlssberg.Motive.Poker.HandRanks;

public class IsHandPairSpec() : Spec<Hand, HandRank>(
    "Is a Pair hand",
    new HasNPairsSpec(1),
    HandRank.Pair,
    HandRank.HighCard);