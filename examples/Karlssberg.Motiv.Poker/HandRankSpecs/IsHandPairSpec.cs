namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandPairSpec() : Spec<Hand, HandRank>(
    new HasNPairsSpec(1)
        .YieldWhenTrue(HandRank.Pair)
        .YieldWhenFalse(HandRank.HighCard)
        .CreateSpec("is a Pair hand"));