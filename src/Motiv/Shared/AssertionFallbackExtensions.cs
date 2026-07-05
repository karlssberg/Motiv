namespace Motiv.Shared;

internal static class AssertionFallbackExtensions
{
    internal static string ElseFallback(this string? assertion, Func<string> fallback) =>
        string.IsNullOrWhiteSpace(assertion) ? fallback() : assertion!;

    internal static IEnumerable<string> ElseFallback(this IEnumerable<string>? assertions, Func<string> fallback)
    {
        var meaningful = FilterMeaningful(assertions);
        return meaningful.Length > 0 ? meaningful : [fallback()];
    }

    internal static IEnumerable<string> ElseFallback(this IEnumerable<string>? assertions, Func<IEnumerable<string>> fallback)
    {
        var meaningful = FilterMeaningful(assertions);
        return meaningful.Length > 0 ? meaningful : fallback();
    }

    private static string[] FilterMeaningful(IEnumerable<string>? assertions) =>
        assertions?.Where(assertion => !string.IsNullOrWhiteSpace(assertion)).ToArray() ?? [];
}
