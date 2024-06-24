using static Motiv.Poker.HandRank;

namespace Motiv.Poker.HandRankProposition;

public class IsHandTwoPairProposition() : Spec<Hand, HandRank>(
    Spec.Build(new HasNPairsProposition(2))
        .WhenTrue(TwoPair)
        .WhenFalse(Unknown)
        .Create("is a two pair hand"));