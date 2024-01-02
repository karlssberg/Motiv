using Karlssberg.Motiv.Poker.StraightHands;

namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandStraightSpec() : Spec<Hand, HandRank>(
    "Is a Straight hand",
    new IsAceHighStraightBroadwaySpec()
        | new IsKingHighStraightSpec()
        | new IsQueenHighStraightSpec()
        | new IsJackHighStraightSpec()
        | new IsTenHighStraightSpec()
        | new IsNineHighStraightSpec()
        | new IsEightHighStraightSpec()
        | new IsSevenHighStraightSpec()
        | new IsSixHighStraightSpec()
        | new IsFiveHighStraightWheelOrBicycleSpec(),
    HandRank.Straight,
    HandRank.HighCard)
{
}