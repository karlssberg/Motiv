using System.Reflection;
using System.Runtime.Versioning;

namespace Motiv.Testing;

/// <summary>
/// What each <c>TargetAssetTests</c> asks of an assembly: which build of it was loaded, and whether
/// everything that build references can be found. Shared as linked source by every test project
/// that opts into the netstandard2.0 asset leg (#250).
/// </summary>
internal static class TargetAsset
{
    /// <summary>
    /// The framework a referenced project's build should target on this leg. The asset leg and
    /// <c>net472</c> both load the <c>netstandard2.0</c> build; every other leg loads the build
    /// matching the test assembly's own framework.
    /// </summary>
    public static string Expected =>
#if MOTIV_NETSTANDARD_ASSET || NETFRAMEWORK
        ".NETStandard,Version=v2.0";
#else
        FrameworkOf(typeof(TargetAsset).Assembly);
#endif

    public static string FrameworkOf(Assembly assembly) =>
        assembly.GetCustomAttribute<TargetFrameworkAttribute>()?.FrameworkName ?? "(no TargetFrameworkAttribute)";

    /// <summary>
    /// NuGet restore does not see <c>SetTargetFramework</c>, so a package the netstandard2.0 build
    /// needs only on that surface is never restored for the asset leg. Listing the references that
    /// fail to load names the missing assembly, instead of leaving it to surface as a type
    /// initializer failure hundreds of tests later.
    /// </summary>
    public static IEnumerable<string> UnresolvedReferencesOf(Assembly assembly) =>
        assembly.GetReferencedAssemblies()
            .Where(reference => !Resolves(reference))
            .Select(reference => reference.FullName);

    private static bool Resolves(AssemblyName reference)
    {
        try
        {
            Assembly.Load(reference);
            return true;
        }
        catch (FileNotFoundException)
        {
            return false;
        }
    }
}
