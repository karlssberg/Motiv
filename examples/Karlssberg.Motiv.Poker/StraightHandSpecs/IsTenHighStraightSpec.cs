namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsTenHighStraightSpec() : Spec<Hand>(
    Spec.Build(new DoesHandContainSpecifiedRanksSpec(TenHighStraight))
        .WhenTrue("is Ten High Straight")
        .WhenFalse("is Not Ten High Straight")
        .Create())
{
    private static readonly ICollection<Rank> TenHighStraight = [Rank.Ten, Rank.Nine, Rank.Eight, Rank.Seven, Rank.Six];
}