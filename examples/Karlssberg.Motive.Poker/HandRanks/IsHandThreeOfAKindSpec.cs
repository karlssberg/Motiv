namespace Karlssberg.Motive.Poker.HandRanks;

public class IsHandThreeOfAKindSpec() : Spec<Hand, HandRank>(
    "Is a Three of a Kind hand",
    new HasNCardsWithTheSameRankSpec(3),
    HandRank.ThreeOfAKind,
    HandRank.HighCard);