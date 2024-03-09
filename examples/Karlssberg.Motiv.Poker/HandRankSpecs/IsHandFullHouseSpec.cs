namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandFullHouseSpec() : Spec<Hand, HandRank>(() =>
    Spec.Build(new HasNCardsWithTheSameRankSpec(2) & new HasNCardsWithTheSameRankSpec(3))
        .WhenTrue(HandRank.FullHouse)
        .WhenFalse(HandRank.Unknown)
        .CreateSpec("is a full house hand"));