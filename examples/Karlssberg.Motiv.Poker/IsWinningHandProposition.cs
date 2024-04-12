using Karlssberg.Motiv.Poker.HandRankProposition;

namespace Karlssberg.Motiv.Poker;

public class IsWinningHandProposition() : Spec<Hand, HandRank>(
    Spec
        .Build(new IsHandPairProposition()
             | new IsHandTwoPairProposition()
             | new IsHandThreeOfAKindProposition()
             | new IsHandStraightProposition()
             | new IsHandFlushProposition()
             | new IsHandFullHouseProposition()
             | new IsHandFourOfAKindProposition()
             | new IsHandStraightFlushProposition()
             | new IsHandRoyalFlushProposition())
        .WhenTrue((_, result) => result.Metadata)
        .WhenFalse(HandRank.HighCard)
        .Create("is a winning poker hand"));