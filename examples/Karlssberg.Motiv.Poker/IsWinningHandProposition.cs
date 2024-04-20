using Karlssberg.Motiv.Poker.HandRankProposition;

namespace Karlssberg.Motiv.Poker;

public class IsWinningHandProposition() : Spec<Hand, HandRank>(
    Spec.Build(new Spec<Hand, HandRank>[] 
            {
                new IsHandRoyalFlushProposition(),
                new IsHandStraightFlushProposition(),
                new IsHandFourOfAKindProposition(),
                new IsHandFullHouseProposition(),
                new IsHandFlushProposition(),
                new IsHandStraightProposition(),
                new IsHandThreeOfAKindProposition(),
                new IsHandTwoPairProposition(),
                new IsHandPairProposition(),
            }
            .OrElseTogether())
        .WhenTrue((_, result) => result.Metadata)
        .WhenFalse(HandRank.HighCard)
        .Create("is a winning poker hand"));