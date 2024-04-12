using Karlssberg.Motiv.Poker.StraightHandProposition;

namespace Karlssberg.Motiv.Poker.HandRankProposition;

public class IsHandRoyalFlushProposition() : Spec<Hand, HandRank>(() => Spec
    .Build(new IsHandFlushProposition() & new IsAceHighStraightBroadwayProposition())
    .WhenTrue(HandRank.RoyalFlush)
    .WhenFalse(HandRank.Unknown)
    .Create("is a royal flush hand"));