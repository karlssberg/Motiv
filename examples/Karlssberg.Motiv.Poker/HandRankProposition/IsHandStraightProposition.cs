using Karlssberg.Motiv.Poker.StraightHandProposition;

namespace Karlssberg.Motiv.Poker.HandRankProposition;

public class IsHandStraightProposition() : Spec<Hand, HandRank>(() => 
    Spec.Build(
    new IsAceHighStraightBroadwayProposition()
       | new IsKingHighStraightProposition()
       | new IsQueenHighStraightProposition()
       | new IsJackHighStraightProposition()
       | new IsTenHighStraightProposition()
       | new IsNineHighStraightProposition()
       | new IsEightHighStraightProposition()
       | new IsSevenHighStraightProposition()
       | new IsSixHighStraightProposition()
       | new IsFiveHighStraightWheelOrBicycleProposition())
    .WhenTrue(HandRank.Straight)
    .WhenFalse(HandRank.Unknown)
    .Create("is a straight hand"));