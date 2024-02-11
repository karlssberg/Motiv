using static Karlssberg.Motiv.Poker.HandRank;

namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandTwoPairSpec() : Spec<Hand, HandRank>(
    Spec.Extend(new HasNPairsSpec(2))
        .WhenTrue(TwoPair)
        .WhenFalse(HighCard)
        .CreateSpec("is a two pair hand"));