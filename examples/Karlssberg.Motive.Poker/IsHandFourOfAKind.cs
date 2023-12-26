namespace Karlssberg.Motive.Poker;

public class IsHandFourOfAKind() : Specification<Hand, HandRank>(
    new IsMaxCardsWithTheSameRank(4),
    HandRank.FourOfAKind,
    HandRank.HighCard);