using Karlssberg.Motiv.Poker.StraightHands;

namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandRoyalFlushSpec() : Spec<Hand, HandRank>(
    "Is Royal Flush",
    new IsHandFlushSpec() & new IsAceHighStraightBroadwaySpec().ChangeMetadata(HandRank.Straight, HandRank.HighCard),
    HandRank.RoyalFlush,
    HandRank.HighCard);