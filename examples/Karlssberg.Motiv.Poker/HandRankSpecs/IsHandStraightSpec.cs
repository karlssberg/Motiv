using Karlssberg.Motiv.Poker.StraightHandSpecs;

namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandStraightSpec() : Spec<Hand, HandRank>(() =>
{
    var isStraightHand = new IsAceHighStraightBroadwaySpec()
        | new IsKingHighStraightSpec()
        | new IsQueenHighStraightSpec()
        | new IsJackHighStraightSpec()
        | new IsTenHighStraightSpec()
        | new IsNineHighStraightSpec()
        | new IsEightHighStraightSpec()
        | new IsSevenHighStraightSpec()
        | new IsSixHighStraightSpec()
        | new IsFiveHighStraightWheelOrBicycleSpec();

    return isStraightHand
        .YieldWhenTrue(HandRank.Straight)
        .YieldWhenFalse(HandRank.HighCard);
});