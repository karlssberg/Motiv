namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandFullHouseSpec() : Spec<Hand, HandRank>(() =>
    Spec.Build(new HasNCardsWithTheSameRankSpec(2) & new HasNCardsWithTheSameRankSpec(3))
        .YieldWhenTrue(HandRank.FullHouse)
        .YieldWhenFalse(HandRank.HighCard)
        .CreateSpec("is a full house hand"));