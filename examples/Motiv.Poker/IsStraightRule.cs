using Motiv.Poker.Models;
using Motiv.Poker.StraightHand;

namespace Motiv.Poker;

public class IsStraightRule() : Policy<Hand, HandRank>(
    Spec.Build(StraightPropositions.OrTogether())
        .WhenTrue(HandRank.Straight)
        .WhenFalse(HandRank.HighCard)
        .Create("is a straight hand"))
{
    private static readonly Spec<Hand>[] StraightPropositions =
    [
        new IsAceHighStraightBroadwayProposition(),
        new IsKingHighStraightProposition(),
        new IsQueenHighStraightProposition(),
        new IsJackHighStraightProposition(),
        new IsTenHighStraightProposition(),
        new IsNineHighStraightProposition(),
        new IsEightHighStraightProposition(),
        new IsSevenHighStraightProposition(),
        new IsSixHighStraightProposition(),
        new IsFiveHighStraightWheelOrBicycleProposition()
    ];



}
