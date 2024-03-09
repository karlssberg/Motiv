using Karlssberg.Motiv.Poker.StraightHandSpecs;

namespace Karlssberg.Motiv.Poker.HandRankSpecs;

public class IsHandStraightSpec() : Spec<Hand, HandRank>(() => 
    Spec.Build(
    new IsAceHighStraightBroadwaySpec()
       | new IsKingHighStraightSpec()
       | new IsQueenHighStraightSpec()
       | new IsJackHighStraightSpec()
       | new IsTenHighStraightSpec()
       | new IsNineHighStraightSpec()
       | new IsEightHighStraightSpec()
       | new IsSevenHighStraightSpec()
       | new IsSixHighStraightSpec()
       | new IsFiveHighStraightWheelOrBicycleSpec())
    .WhenTrue(HandRank.Straight)
    .WhenFalse(HandRank.Unknown)
    .CreateSpec("is a straight hand"));