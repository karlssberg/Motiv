namespace Motiv.Poker.HandRankProposition;

public class IsHandThreeOfAKindProposition() : Spec<Hand, HandRank>(
    Spec.Build(new HasNCardsWithTheSameRankProposition(3))
        .WhenTrue(HandRank.ThreeOfAKind)
        .WhenFalse(HandRank.HighCard)
        .Create("is a three of a kind hand"));