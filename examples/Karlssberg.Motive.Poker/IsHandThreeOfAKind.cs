namespace Karlssberg.Motive.Poker;

public class IsHandThreeOfAKind() : Specification<Hand, HandRank>(
    new IsMaxCardsWithTheSameRank(3),
    HandRank.ThreeOfAKind,
    HandRank.HighCard);