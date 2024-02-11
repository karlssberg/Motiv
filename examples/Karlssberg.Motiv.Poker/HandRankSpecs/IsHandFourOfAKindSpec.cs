namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandFourOfAKindSpec() : Spec<Hand, HandRank>(
    Spec.Build(new HasNCardsWithTheSameRankSpec(4))
        .YieldWhenTrue(HandRank.FourOfAKind)
        .YieldWhenFalse(HandRank.HighCard)
        .CreateSpec("is a four of a kind hand"));