using Microsoft.CodeAnalysis.CSharp;
using Motiv.CodeFix.Syntax;
using Shouldly;

namespace Motiv.CodeFix.Tests;

public class ClauseSetTests
{
    [Fact]
    public void Should_suffix_a_name_that_two_distinct_clauses_derive()
    {
        var first = SyntaxFactory.ParseExpression("Equals(a, b)");
        var second = SyntaxFactory.ParseExpression("Equals(b, c)");

        var names = new ClauseSet([(first.ToString(), first, first), (second.ToString(), second, second)])
            .UniqueClauses.Values
            .Select(clause => clause.DerivedName)
            .ToList();

        names[1].ShouldBe($"{names[0]}2");
    }

    [Fact]
    public void Should_resolve_a_repeated_clause_to_the_name_of_its_first_occurrence()
    {
        var clause = SyntaxFactory.ParseExpression("a > 0");
        var other = SyntaxFactory.ParseExpression("b > 0");

        var composition = new ClauseSet([(clause.ToString(), clause, clause), (other.ToString(), other, other), (clause.ToString(), clause, clause)])
            .ResolveComposition(SyntaxFactory.ParseExpression(
                $"{ClauseSet.Placeholder(1)}.AndAlso({ClauseSet.Placeholder(2)}).OrElse({ClauseSet.Placeholder(3)})"));

        composition.ToString().ShouldBe("isAPositive.AndAlso(isBPositive).OrElse(isAPositive)");
    }
}
