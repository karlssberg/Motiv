// This file exists solely so positional records, `init` setters, and `required` members compile
// for netstandard2.0 — the C# compiler emits references to these types regardless of the target
// framework, and netstandard2.0 predates all three language features. None of this is behavior:
// every type here is an empty marker the compiler looks for by name and namespace.
//
// These must stay internal. On every other target framework (net8.0, net9.0, net10.0) the real
// BCL types already exist and must win; a public polyfill here would collide with them instead of
// silently losing to them via the compiler's "define only if absent" resolution, breaking every
// other target framework this project builds for.
#if NETSTANDARD2_0

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Marker type the compiler looks for to allow `init` accessors. Presence alone is enough —
    /// nothing calls into it.
    /// </summary>
    internal static class IsExternalInit
    {
    }

    /// <summary>Marker attribute the compiler emits on a member declared `required`.</summary>
    [AttributeUsage(
        AttributeTargets.Class | AttributeTargets.Struct | AttributeTargets.Field | AttributeTargets.Property,
        AllowMultiple = false,
        Inherited = false)]
    internal sealed class RequiredMemberAttribute : Attribute
    {
    }

    /// <summary>
    /// Marker attribute the compiler emits on any type or member that uses a compiler feature
    /// requiring runtime/consumer awareness — `required` members being the one this project needs.
    /// </summary>
    [AttributeUsage(AttributeTargets.All, AllowMultiple = true, Inherited = false)]
    internal sealed class CompilerFeatureRequiredAttribute(string featureName) : Attribute
    {
        public string FeatureName { get; } = featureName;

        public bool IsOptional { get; init; }

        /// <summary>The canonical feature name the compiler uses for `required` members.</summary>
        public const string RequiredMembers = nameof(RequiredMembers);
    }
}

#endif
