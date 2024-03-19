namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandStraightFlushSpec() : Spec<Hand, HandRank>(
    Spec.Build(new IsHandStraightSpec() & new IsHandFlushSpec())
        .WhenTrue(HandRank.StraightFlush)
        .WhenFalse(HandRank.Unknown)
        .Create("is a straight flush hand"));