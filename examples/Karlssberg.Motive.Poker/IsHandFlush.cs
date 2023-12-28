namespace Karlssberg.Motive.Poker;

public class IsHandFlush() : Spec<Hand, HandRank>(
    new IsMaxCardsWithTheSameSuit(5),
    HandRank.Flush,
    HandRank.HighCard);