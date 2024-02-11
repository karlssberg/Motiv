using Karlssberg.Motiv.Poker.StraightHandSpecs;

namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandRoyalFlushSpec() : Spec<Hand, HandRank>(() => Spec
    .Extend(new IsHandFlushSpec().ToSimpleSpec() & new IsAceHighStraightBroadwaySpec())
    .WhenTrue(HandRank.RoyalFlush)
    .WhenFalse(HandRank.HighCard)
    .CreateSpec("is a royal flush hand"));