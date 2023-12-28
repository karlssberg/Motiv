namespace Karlssberg.Motive.Poker;

public class IsHandFourOfAKind() : Spec<Hand, HandRank>(
    new IsMaxCardsWithTheSameRank(4),
    HandRank.FourOfAKind,
    HandRank.HighCard);