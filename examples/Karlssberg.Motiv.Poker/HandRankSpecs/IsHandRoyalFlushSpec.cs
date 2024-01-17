using Karlssberg.Motiv.Poker.StraightHandSpecs;

namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandRoyalFlushSpec() : Spec<Hand, HandRank>(() =>
{
    var isFlush = new IsHandFlushSpec();
    
    var isAceHighStraight = new IsAceHighStraightBroadwaySpec()
        .YieldWhenTrue(HandRank.Straight)
        .YieldWhenFalse(HandRank.HighCard)
        .CreateSpec("is Ace High Straight");
    
    var isRoyalFlush = isFlush & isAceHighStraight;
    
    return isRoyalFlush
            .YieldWhenTrue(HandRank.RoyalFlush)
            .YieldWhenFalse(HandRank.HighCard)
            .CreateSpec("is Royal Flush");
});