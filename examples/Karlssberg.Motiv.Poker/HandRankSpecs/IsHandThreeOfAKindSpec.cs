namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandThreeOfAKindSpec() : Spec<Hand, HandRank>(
    new HasNCardsWithTheSameRankSpec(3)
        .YieldWhenTrue(HandRank.ThreeOfAKind)
        .YieldWhenFalse(HandRank.HighCard)
        .CreateSpec("is a Three of a Kind hand"));