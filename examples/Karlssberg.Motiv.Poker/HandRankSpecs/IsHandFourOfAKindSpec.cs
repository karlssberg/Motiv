namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandFourOfAKindSpec() : Spec<Hand, HandRank>(
    new HasNCardsWithTheSameRankSpec(4)
        .YieldWhenTrue(HandRank.FourOfAKind)
        .YieldWhenFalse(HandRank.HighCard));