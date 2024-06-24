namespace Motiv.Poker.HandRankProposition;

public class IsHandPairProposition() : Spec<Hand, HandRank>(
    Spec.Build(new HasNPairsProposition(1))
        .WhenTrue(HandRank.Pair)
        .WhenFalse(HandRank.Unknown)
        .Create("is a pair hand"));