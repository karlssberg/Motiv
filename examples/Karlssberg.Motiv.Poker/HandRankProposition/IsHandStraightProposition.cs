using Karlssberg.Motiv.Poker.StraightHandProposition;

namespace Karlssberg.Motiv.Poker.HandRankProposition;

public class IsHandStraightProposition() : Spec<Hand, HandRank>( 
    Spec.Build(new Spec<Hand>[] 
        {
            new IsAceHighStraightBroadwayProposition(),
            new IsKingHighStraightProposition(),
            new IsQueenHighStraightProposition(),
            new IsJackHighStraightProposition(),
            new IsTenHighStraightProposition(),
            new IsNineHighStraightProposition(),
            new IsEightHighStraightProposition(),
            new IsSevenHighStraightProposition(),
            new IsSixHighStraightProposition(),
            new IsFiveHighStraightWheelOrBicycleProposition(),
        }
        .OrTogether())
    .WhenTrue(HandRank.Straight)
    .WhenFalse(HandRank.Unknown)
    .Create("is a straight hand"));