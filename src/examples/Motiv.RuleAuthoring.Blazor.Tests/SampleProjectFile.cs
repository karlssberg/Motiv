namespace Motiv.RuleAuthoring.Blazor.Tests;

/// <summary>Locates the sample's project file from the test assembly's build output.</summary>
internal static class SampleProjectFile
{
    private const string ProjectName = "Motiv.RuleAuthoring.Blazor";

    /// <summary>The absolute path of the sample's <c>.csproj</c>.</summary>
    /// <exception cref="InvalidOperationException">The sample project could not be found.</exception>
    public static string Path { get; } = Locate();

    private static string Locate()
    {
        var relative = System.IO.Path.Combine(ProjectName, $"{ProjectName}.csproj");

        for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
             directory is not null;
             directory = directory.Parent)
        {
            var candidate = System.IO.Path.Combine(directory.FullName, relative);
            if (File.Exists(candidate))
                return candidate;
        }

        throw new InvalidOperationException(
            $"Could not find {relative} above {AppContext.BaseDirectory}. The gates in " +
            $"{nameof(SampleIsSelfContainedTests)} check the sample's source tree, so a test run " +
            "that cannot reach it must fail rather than pass vacuously.");
    }
}
