using Karlssberg.Motiv.Poker.StraightHandSpecs;

namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandRoyalFlushSpec() : Spec<Hand, HandRank>(() => Spec
    .Build(new IsHandFlushSpec().ToSimpleSpec() & new IsAceHighStraightBroadwaySpec())
    .YieldWhenTrue(HandRank.RoyalFlush)
    .YieldWhenFalse(HandRank.HighCard)
    .CreateSpec("is a royal flush hand"));