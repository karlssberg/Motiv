using Karlssberg.Motive.Poker.StraightHands;

namespace Karlssberg.Motive.Poker.HandRanks;

public class IsHandRoyalFlushSpec() : Spec<Hand, HandRank>(
    "Is Royal Flush",
    new IsHandFlushSpec() & new IsAceHighStraightBroadwaySpec().ChangeMetadata(HandRank.Straight, HandRank.HighCard),
    HandRank.RoyalFlush,
    HandRank.HighCard);