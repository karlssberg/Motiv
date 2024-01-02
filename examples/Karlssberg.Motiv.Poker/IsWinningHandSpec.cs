using Karlssberg.Motiv.Poker.HandRankSpecs;
using Karlssberg.Motiv.Poker.StraightHands;

namespace Karlssberg.Motiv.Poker;

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