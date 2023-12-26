namespace Karlssberg.Motive.Poker;

public class IsWinningHand() : Specification<Hand, HandRank>(
    new IsHandPair() 
      | new IsHandTwoPair() 
      | new IsHandThreeOfAKind() 
      | new IsHandStraight() 
      | new IsHandFlush() 
      | new IsHandFullHouse() 
      | new IsHandFourOfAKind() 
      | new IsHandStraightFlush()
      | new IsHandRoyalFlush());