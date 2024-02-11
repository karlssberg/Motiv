using Karlssberg.Motiv.Poker.StraightHandSpecs;

namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandRoyalFlushSpec() : Spec<Hand, HandRank>(() =>
{
    var isFlush = new IsHandFlushSpec();

    var isAceHighStraight = new IsAceHighStraightBroadwaySpec()
        .YieldWhenTrue(HandRank.Straight)
        .YieldWhenFalse(HandRank.HighCard);

    return Spec
        .Build(isFlush & isAceHighStraight)
        .YieldWhenTrue(HandRank.RoyalFlush)
        .YieldWhenFalse(HandRank.HighCard)
        .CreateSpec("is a royal flush hand");
});