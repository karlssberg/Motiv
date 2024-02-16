namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandPairSpec() : Spec<Hand, HandRank>(
    Spec.Build(new HasNPairsSpec(1))
        .WhenTrue(HandRank.Pair)
        .WhenFalse(HandRank.HighCard)
        .CreateSpec("is a pair hand"));