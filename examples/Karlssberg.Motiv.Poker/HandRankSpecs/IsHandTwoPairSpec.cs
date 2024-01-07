using static Karlssberg.Motiv.Poker.HandRank;

namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandTwoPairSpec() : Spec<Hand, HandRank>(
    new HasNPairsSpec(2)
        .YieldWhenTrue(TwoPair)
        .YieldWhenFalse(HighCard)
        .CreateSpec("is a Two Pair hand"));