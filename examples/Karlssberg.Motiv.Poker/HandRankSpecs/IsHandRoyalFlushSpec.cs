using Karlssberg.Motiv.Poker.StraightHandSpecs;

namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandRoyalFlushSpec() : Spec<Hand, HandRank>(() => Spec
    .Build(new IsHandFlushSpec() & new IsAceHighStraightBroadwaySpec())
    .WhenTrue(HandRank.RoyalFlush)
    .WhenFalse(HandRank.Unknown)
    .Create("is a royal flush hand"));