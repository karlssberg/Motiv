namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandStraightFlushSpec() : Spec<Hand, HandRank>(
    "Is a Straight Flush hand",
    new IsHandStraightSpec() & new IsHandFlushSpec(),
    HandRank.StraightFlush,
    HandRank.HighCard);