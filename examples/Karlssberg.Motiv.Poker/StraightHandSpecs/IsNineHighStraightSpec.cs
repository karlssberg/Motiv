namespace Karlssberg.Motiv.Poker.StraightHandSpecs;

public class IsNineHighStraightSpec() : Spec<Hand>(
    Spec.Build(new DoesHandContainSpecifiedRanksSpec(NineHighStraight))
        .WhenTrue("is Nine High Straight")
        .WhenFalse("is Not Nine High Straight")
        .CreateSpec())
{
    private static readonly ICollection<Rank> NineHighStraight = [Rank.Nine, Rank.Eight, Rank.Seven, Rank.Six, Rank.Five];
}