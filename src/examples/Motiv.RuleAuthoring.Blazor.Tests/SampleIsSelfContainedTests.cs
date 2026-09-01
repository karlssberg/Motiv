using System.Text.RegularExpressions;
using System.Xml.Linq;
using Shouldly;

namespace Motiv.RuleAuthoring.Blazor.Tests;

/// <summary>
/// Spec 4 §7 asks that the Blazor sample author a valid rule document "through
/// <c>Motiv.Serialization</c> alone (no <c>rules-core</c>)". These are the two ways that stops being
/// true without a single test going red on its own.
/// </summary>
public class SampleIsSelfContainedTests
{
    private static readonly string[] AllowedProjectReferences = ["Motiv.Serialization.csproj"];

    private static readonly string[] AllowedPackageReferences =
    [
        "Microsoft.AspNetCore.Components.WebAssembly",
        "Microsoft.AspNetCore.Components.WebAssembly.DevServer"
    ];

    /// <remarks>
    /// The cheap ways to break §7 are a reference to <c>Motiv.Serialization.AspNetCore</c> — which
    /// carries <c>InternalsVisibleTo</c> and would hand the sample a document model no adopter has —
    /// or a reach into <c>Motiv.Studio</c>. Either would leave the sample demonstrating a surface
    /// wider than the one the tier table sells.
    /// </remarks>
    [Fact]
    public void References_nothing_beyond_Motiv_Serialization_and_the_Blazor_host()
    {
        var project = XDocument.Load(SampleProjectFile.Path);

        // Set equality, not a subset: a subset assertion also passes when the read returns nothing,
        // which is a gate that reports a property it is no longer checking.
        References(project, "ProjectReference")
            .ShouldBe(AllowedProjectReferences, ignoreOrder: true);
        References(project, "PackageReference")
            .ShouldBe(AllowedPackageReferences, ignoreOrder: true);
    }

    /// <remarks>
    /// A C# project cannot accidentally acquire <c>@motiv-rules/core</c>, but a sample can quietly
    /// acquire one — so this looks over the whole project, not just <c>wwwroot</c>. A collocated
    /// <c>Author.razor.js</c>, Blazor's own JS-isolation shape, never appears in <c>wwwroot</c> at
    /// all and is copied to the output at build.
    /// </remarks>
    [Fact]
    public void Ships_no_JavaScript_of_its_own()
    {
        JavaScriptFilesInSampleSource().ShouldBeEmpty();
    }

    /// <remarks>
    /// The other half, and the one no file listing can see: a <c>&lt;script src="https://…"&gt;</c>
    /// adds no <c>.js</c> file to the tree and would leave the sample loading a rules package over
    /// the network while every job stayed green.
    /// </remarks>
    [Fact]
    public void Loads_no_script_but_the_Blazor_runtime()
    {
        var html = File.ReadAllText(Path.Combine(SampleDirectory, "wwwroot", "index.html"));

        ScriptSources(html).ShouldBe(["_framework/blazor.webassembly.js"]);
    }

    private static IEnumerable<string> JavaScriptFilesInSampleSource() =>
        Directory.EnumerateFiles(SampleDirectory, "*.js", SearchOption.AllDirectories)
            .Where(file => !IsBuildOutput(file))
            .Select(file => Path.GetRelativePath(SampleDirectory, file));

    private static bool IsBuildOutput(string file) =>
        Path.GetRelativePath(SampleDirectory, file)
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)[0]
            is "bin" or "obj";

    private static IReadOnlyList<string> ScriptSources(string html) =>
        [.. Regex.Matches(html, """<script[^>]*\ssrc\s*=\s*["']([^"']+)["']""", RegexOptions.IgnoreCase)
            .Select(match => match.Groups[1].Value)];

    private static string SampleDirectory { get; } =
        Path.GetDirectoryName(SampleProjectFile.Path)!;

    private static IEnumerable<string> References(XDocument project, string element) =>
        project.Descendants(element)
            .Select(reference => reference.Attribute("Include")?.Value ?? "")
            .Select(include => include.Split('\\', '/')[^1]);
}
