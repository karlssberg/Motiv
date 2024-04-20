using static Karlssberg.Motiv.Poker.HandRank;

namespace Karlssberg.Motiv.Poker.HandRankProposition;

public class IsHandTwoPairProposition() : Spec<Hand, HandRank>(
    Spec.Build(new HasNPairsProposition(2))
        .WhenTrue(TwoPair)
        .WhenFalse(HandRank.Unknown)
        .Create("is a two pair hand"));