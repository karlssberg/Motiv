namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandFourOfAKindSpec() : Spec<Hand, HandRank>(
    "Is a Four of a Kind hand",
    new HasNCardsWithTheSameRankSpec(4),
    HandRank.FourOfAKind,
    HandRank.HighCard);