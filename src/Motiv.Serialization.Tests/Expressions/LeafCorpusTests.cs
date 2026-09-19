#if NET8_0_OR_GREATER
using System.Text.Json;
using Motiv.Serialization.Expressions;

namespace Motiv.Serialization.Tests.Expressions;

/// <summary>
/// Runs the conformance corpus (<c>expression/corpus.json</c>, shared with
/// <c>ui/packages/rules-core/test/expression-corpus.test.ts</c>) against the C# checker. A case
/// passes on both sides or CI fails.
/// </summary>
public class LeafCorpusTests
{
    private static readonly JsonDocument Corpus =
        JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "expression", "corpus.json")));

    public static IEnumerable<object[]> Cases() =>
        Corpus.RootElement.GetProperty("cases").EnumerateArray()
            .Select(c => new object[] { c.GetProperty("name").GetString()! });

    private static RuleParameterType ParseParameterType(string value) => value switch
    {
        "integer" => RuleParameterType.Integer,
        "number" => RuleParameterType.Number,
        "string" => RuleParameterType.String,
        "boolean" => RuleParameterType.Boolean,
        _ => throw new ArgumentOutOfRangeException(nameof(value), value, "unknown parameter type"),
    };

    private static RuleParameterDeclaration[] Parameters() =>
        Corpus.RootElement.GetProperty("parameters").EnumerateObject()
            .Select(p => new RuleParameterDeclaration(p.Name, ParseParameterType(p.Value.GetString()!), false, null))
            .ToArray();

    [Theory]
    [MemberData(nameof(Cases))]
    public void Should_agree_with_the_corpus(string name)
    {
        var c = Corpus.RootElement.GetProperty("cases").EnumerateArray().Single(x => x.GetProperty("name").GetString() == name);
        var modelType = c.GetProperty("scope").GetString() == "order" ? typeof(CorpusFixtures.Order) : typeof(CorpusFixtures.Customer);
        var problems = new List<LeafProblem>();
        var root = LeafParser.Parse(c.GetProperty("leaf").GetString()!, problems);
        var analysis = root is null ? null : LeafChecker.Check(root, LeafScope.For(modelType, Parameters()));
        var all = analysis?.Problems ?? problems;
        var errors = all.Where(p => !p.IsWarning).ToList();
        var warnings = all.Where(p => p.IsWarning).ToList();

        warnings.Count.ShouldBe(c.TryGetProperty("warnings", out var w) ? w.GetInt32() : 0);

        if (c.TryGetProperty("problems", out var expected))
        {
            errors.Select(e => e.Code.ToString()).ShouldBe(expected.EnumerateArray().Select(p => p.GetProperty("code").GetString()));
            var i = 0;
            foreach (var p in expected.EnumerateArray())
            {
                if (p.TryGetProperty("contains", out var fragments))
                    foreach (var fragment in fragments.EnumerateArray()) errors[i].Message.ShouldContain(fragment.GetString()!);
                if (p.TryGetProperty("range", out var range))
                    (errors[i].Start, errors[i].End).ShouldBe((range[0].GetInt32(), range[1].GetInt32()));
                i++;
            }
            return;
        }

        errors.ShouldBeEmpty();
        if (c.TryGetProperty("facts", out var facts))
            foreach (var f in facts.EnumerateArray())
            {
                // Mirrors the TS runner's `.find` (first match, not a uniqueness assertion) — a case
                // like "1 + 1 > 1" has two literal `1` facts sharing the same printed text.
                var text = f.GetProperty("text").GetString();
                var fact = analysis!.Facts.FirstOrDefault(x => x.Node is not Binary && LeafChecker.Print(x.Node) == text)
                    .ShouldNotBeNull($"fact for {text}");
                CorpusFixtures.TypeName(fact.Type).ShouldBe(f.GetProperty("type").GetString()!);
                if (f.TryGetProperty("from", out var from)) fact.From.ShouldNotBeNull().ShouldBe(from.GetString()!);
            }
        if (c.TryGetProperty("result", out var result))
            CorpusFixtures.TypeName(analysis!.Types[root!]).ShouldBe(result.GetString()!);
    }
}
#endif
