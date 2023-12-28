namespace Karlssberg.Motive.Poker;

public class IsHandThreeOfAKind() : Spec<Hand, HandRank>(
    new IsMaxCardsWithTheSameRank(3),
    HandRank.ThreeOfAKind,
    HandRank.HighCard);