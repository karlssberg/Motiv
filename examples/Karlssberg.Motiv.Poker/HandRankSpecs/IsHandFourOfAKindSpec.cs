namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandFourOfAKindSpec() : Spec<Hand, HandRank>(
    new HasNCardsWithTheSameRankSpec(4)
        .YieldWhenTrue(HandRank.FourOfAKind)
        .YieldWhenFalse(HandRank.HighCard)
        .CreateSpec("is a Four of a Kind hand"));