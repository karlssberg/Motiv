namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandStraightFlushSpec() : Spec<Hand, HandRank>(
    (new IsHandStraightSpec() & new IsHandFlushSpec())
    .YieldWhenTrue(HandRank.StraightFlush)
    .YieldWhenFalse(HandRank.HighCard));