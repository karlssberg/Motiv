using System.Security.Cryptography;
using System.Text;

namespace Motiv.Tests.Traversal;

/// <summary>
/// The oracle for the description tree. <c>Reason</c> and <c>Justification</c> are produced by twelve
/// bespoke formatters over private state, so the recursion cannot be copied into the test project the
/// way the result-tree walks can. Instead the rendering of the whole generated corpus is pinned by
/// hash, captured from the recursive code before Spec 3A replaced it.
/// </summary>
/// <remarks>
/// A hash rather than the text itself, because the corpus renders to roughly 600 KB. Debuggability is
/// kept by the corpus being seeded: a failure names the seed, and the test prints the rendering it
/// got, so the case is reproducible and readable without the baseline holding it.
/// </remarks>
public class DescriptionBaselineTests
{
    private const int SeedCount = 150;
    private const string BaselineResource = "Motiv.Tests.Traversal.DescriptionBaseline.txt";

    [Fact]
    public void Should_render_every_generated_tree_exactly_as_the_recursive_formatters_did()
    {
        var baseline = ReadBaseline();

        baseline.Count.ShouldBe(SeedCount, "the baseline must cover the whole corpus");

        // Indexed rather than deconstructed: net472 has no Deconstruct for KeyValuePair.
        foreach (var entry in baseline)
        {
            var rendering = Render(entry.Key);

            Hash(rendering).ShouldBe(
                entry.Value,
                $"the description tree renders differently from the recursive formatters at seed {entry.Key}:{Environment.NewLine}{rendering}");
        }
    }

    /// <remarks>
    /// Line endings are normalised to <c>\n</c>. <c>Justification</c> joins its lines with
    /// <see cref="Environment.NewLine" />, so an un-normalised rendering hashes differently on Windows
    /// than on Linux and the baseline would pin the platform rather than the formatters.
    /// </remarks>
    internal static string Render(int seed)
    {
        var rendering = new StringBuilder();

        foreach (var root in ResultTreeGenerator.Corpus(seed))
        foreach (var node in ResultTreeGenerator.Nodes(root))
        {
            rendering.Append(node.Reason).Append('\n');
            rendering.Append(node.Justification.Replace("\r\n", "\n")).Append('\n');
            rendering.Append("--").Append('\n');
        }

        return rendering.ToString();
    }

    [Fact]
    public void Should_render_the_corpus_independently_of_the_platforms_line_ending()
    {
        Render(1).ShouldNotContain(
            "\r",
            customMessage: "the baseline pins the formatters, not the platform — a carriage return in " +
                           "the rendering means the same corpus hashes differently on Windows and Linux");
    }

    internal static string Hash(string rendering)
    {
        using var sha = SHA256.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(rendering));

        var hex = new StringBuilder(hash.Length * 2);
        foreach (var b in hash)
            hex.Append(b.ToString("x2"));

        return hex.ToString();
    }

    private static Dictionary<int, string> ReadBaseline()
    {
        using var stream = typeof(DescriptionBaselineTests).Assembly.GetManifestResourceStream(BaselineResource)
            ?? throw new InvalidOperationException($"{BaselineResource} is not embedded in the test assembly.");

        using var reader = new StreamReader(stream);

        var baseline = new Dictionary<int, string>();

        while (reader.ReadLine() is { } line)
        {
            if (line.Length == 0 || line[0] == '#')
                continue;

            var separator = line.IndexOf(' ');
            baseline[int.Parse(line.Substring(0, separator))] = line.Substring(separator + 1).Trim();
        }

        return baseline;
    }
}
