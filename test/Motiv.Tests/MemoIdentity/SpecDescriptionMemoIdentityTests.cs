using System.Linq.Expressions;
using Motiv.ExpressionTreeProposition;
using Motiv.Shared;
using static Motiv.Tests.MemoIdentity.MemoIdentityPropositions;

namespace Motiv.Tests.MemoIdentity;

/// <summary>
/// The spec descriptions that memoise their statement, their multi-line detail or their reasons return the
/// same string on every read rather than rebuilding it (#294). Each description is given an underlying
/// description, or is an operator over two operands, so its detail runs to more than one line —
/// <see cref="string.Join(string, IEnumerable{string})" /> hands back a lone line as-is, which would make a
/// rebuilt one-line detail indistinguishable from a memoised one.
/// <para>
/// A performance guard, like every test in this folder: a rebuild is an equal value, so these pin the cost
/// of a re-read, not its output (see <see cref="MemoisedResultCase" />).
/// </para>
/// </summary>
public class SpecDescriptionMemoIdentityTests
{
    private static readonly Expression<Func<int, bool>> IsPositive = n => n > 0;

    private static ISpecDescription Underlying => Spec.Build((int n) => n > 0).Create("is positive").Description;

    // These build their reason from the statement on each call; only what they memoise is probed.
    private static readonly string[] ReasonsNotMemoised = ["ToReason(true)", "ToReason(false)"];

    private static readonly (string Name, string DescriptionType, Func<ISpecDescription> Create, string[] NotMemoised)[]
        Descriptions =
        [
            ("statement over an underlying description", "SpecDescription",
                () => new SpecDescription("all positive", Underlying), []),
            ("expression over an underlying description", "ExpressionDescription",
                () => new ExpressionDescription(IsPositive.Body, Underlying), []),
            ("expression tree over an underlying description", "ExpressionTreeDescription",
                () => new ExpressionTreeDescription<int, bool>(IsPositive, Underlying), ReasonsNotMemoised),
            ("expression as a statement over an underlying description", "ExpressionAsStatementDescription",
                () => new ExpressionAsStatementDescription(IsPositive.Body, Underlying), []),
            ("not spec over an operator", "NotSpecDescription",
                () => Proposition("left").And(Proposition("right")).Not().Description, ReasonsNotMemoised),
            ("async not spec over an operator", "AsyncNotSpecDescription",
                () => AsyncProposition("left").And(AsyncProposition("right")).Not().Description, ReasonsNotMemoised),
            ("and spec", "BinarySpecDescription",
                () => Proposition("left").And(Proposition("right")).Description, ReasonsNotMemoised),
            ("async and spec", "AsyncBinarySpecDescription",
                () => AsyncProposition("left").And(AsyncProposition("right")).Description, ReasonsNotMemoised),
        ];

    private static readonly (string Member, Func<ISpecDescription, object> Read)[] Readers =
    [
        ("Statement", description => description.Statement),
        ("Detailed", description => description.Detailed),
        ("ToReason(true)", description => description.ToReason(true)),
        ("ToReason(false)", description => description.ToReason(false)),
    ];

    public static TheoryData<string> DescriptionNames =>
        Descriptions.Select(description => description.Name).ToTheoryData();

    [Theory]
    [MemberData(nameof(DescriptionNames))]
    public void Repeated_reads_return_the_same_statement_detail_and_reasons(string name)
    {
        var (_, descriptionType, create, notMemoised) = Descriptions.Single(description => description.Name == name);

        var description = create();

        description.GetType().NameWithoutArity().ShouldBe(
            descriptionType,
            $"'{name}' no longer builds {descriptionType}; re-point it so that class stays covered");
        description.Detailed.ShouldContain(Environment.NewLine);
        description.ShouldRebuildNothingOnASecondRead(
            Readers.Where(reader => !notMemoised.Contains(reader.Member)),
            name);
    }
}
