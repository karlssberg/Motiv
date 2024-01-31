namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandThreeOfAKindSpec() : Spec<Hand, HandRank>(
    new HasNCardsWithTheSameRankSpec(3)
        .YieldWhenTrue(HandRank.ThreeOfAKind)
        .YieldWhenFalse(HandRank.HighCard));