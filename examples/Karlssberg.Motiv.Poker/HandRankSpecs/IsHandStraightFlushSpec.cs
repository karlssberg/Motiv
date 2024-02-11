namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandStraightFlushSpec() : Spec<Hand, HandRank>(
    Spec.Build(new IsHandStraightSpec() & new IsHandFlushSpec())
        .YieldWhenTrue(HandRank.StraightFlush)
        .YieldWhenFalse(HandRank.HighCard)
        .CreateSpec("is a straight flush hand"));