using Karlssberg.Motive.Poker.HandRankSpecs;
using Karlssberg.Motive.Poker.StraightHands;

namespace Karlssberg.Motive.Poker;

public class IsWinningHandSpec() : Spec<Hand, HandRank>(
    new IsHandPairSpec() 
      | new IsHandTwoPairSpec() 
      | new IsHandThreeOfAKindSpec() 
      | new IsHandStraightSpec() 
      | new IsHandFlushSpec() 
      | new IsHandFullHouseSpec() 
      | new IsHandFourOfAKindSpec() 
      | new IsHandStraightFlushSpec()
      | new IsHandRoyalFlushSpec());