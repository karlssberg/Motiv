namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandStraightFlushSpec() : Spec<Hand, HandRank>(
    Spec.Extend(new IsHandStraightSpec() & new IsHandFlushSpec())
        .WhenTrue(HandRank.StraightFlush)
        .WhenFalse(HandRank.HighCard)
        .CreateSpec("is a straight flush hand"));