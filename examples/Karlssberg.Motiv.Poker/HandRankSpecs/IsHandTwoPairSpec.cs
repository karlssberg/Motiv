using static Karlssberg.Motiv.Poker.HandRank;

namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandTwoPairSpec() : Spec<Hand, HandRank>(
    Spec.Build(new HasNPairsSpec(2))
        .YieldWhenTrue(TwoPair)
        .YieldWhenFalse(HighCard)
        .CreateSpec("is a two pair hand"));