namespace Karlssberg.Motiv.Poker.HandRankProposition;

public class IsHandFullHouseProposition() : Spec<Hand, HandRank>(() =>
    Spec.Build(new HasNCardsWithTheSameRankProposition(2) & new HasNCardsWithTheSameRankProposition(3))
        .WhenTrue(HandRank.FullHouse)
        .WhenFalse(HandRank.Unknown)
        .Create("is a full house hand"));