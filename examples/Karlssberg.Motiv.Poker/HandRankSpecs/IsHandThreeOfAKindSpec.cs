namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandThreeOfAKindSpec() : Spec<Hand, HandRank>(
    Spec.Build(new HasNCardsWithTheSameRankSpec(3))
        .YieldWhenTrue(HandRank.ThreeOfAKind)
        .YieldWhenFalse(HandRank.HighCard)
        .CreateSpec("is a three of a kind hand"));