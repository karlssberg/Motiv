namespace Motiv.Poker.HandRankProposition;

public class IsHandFourOfAKindProposition() : Spec<Hand, HandRank>(
    Spec.Build(new HasNCardsWithTheSameRankProposition(4))
        .WhenTrue(HandRank.FourOfAKind)
        .WhenFalse(HandRank.Unknown)
        .Create("is a four of a kind hand"));