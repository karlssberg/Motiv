namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandFourOfAKindSpec() : Spec<Hand, HandRank>(
    Spec.Build(new HasNCardsWithTheSameRankSpec(4))
        .WhenTrue(HandRank.FourOfAKind)
        .WhenFalse(HandRank.HighCard)
        .CreateSpec("is a four of a kind hand"));