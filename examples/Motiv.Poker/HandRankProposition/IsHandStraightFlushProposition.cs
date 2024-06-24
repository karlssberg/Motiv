namespace Motiv.Poker.HandRankProposition;

public class IsHandStraightFlushProposition() : Spec<Hand, HandRank>(
    Spec.Build(new IsHandStraightProposition() & new IsHandFlushProposition())
        .WhenTrue(HandRank.StraightFlush)
        .WhenFalse(HandRank.Unknown)
        .Create("is a straight flush hand"));