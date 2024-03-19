using Karlssberg.Motiv.Poker.HandRankSpecs;

namespace Karlssberg.Motiv.Poker;

public class IsWinningHandSpec() : Spec<Hand, HandRank>(
    Spec
        .Build(new IsHandPairSpec()
             | new IsHandTwoPairSpec()
             | new IsHandThreeOfAKindSpec()
             | new IsHandStraightSpec()
             | new IsHandFlushSpec()
             | new IsHandFullHouseSpec()
             | new IsHandFourOfAKindSpec()
             | new IsHandStraightFlushSpec()
             | new IsHandRoyalFlushSpec())
        .WhenTrue((_, result) => result.Metadata)
        .WhenFalse(HandRank.HighCard)
        .Create("is a winning poker hand"));