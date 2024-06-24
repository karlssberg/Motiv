using Motiv.Poker.StraightHandProposition;

namespace Motiv.Poker.HandRankProposition;

public class IsHandStraightProposition() : Spec<Hand, HandRank>(CreateSpec())
{
    private static SpecBase<Hand, HandRank> CreateSpec()
    {
        return Spec.Build(StraightPropositions.OrTogether())
            .WhenTrue(HandRank.Straight)
            .WhenFalse(HandRank.Unknown)
            .Create("is a straight hand");
    }

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