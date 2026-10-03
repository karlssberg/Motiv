namespace Motiv.Tests;

/// <summary>Counts how often a caller-supplied delegate runs, by passing its return value through <see cref="Pass{T}" />.</summary>
internal sealed class CallCounter
{
    public int Calls { get; private set; }

    public T Pass<T>(T value)
    {
        Calls++;
        return value;
    }
}

internal static class TestHelperExtensions
{
    /// <summary>The type's name without its generic arity (<c>AndSpec`2</c> becomes <c>AndSpec</c>).</summary>
    public static string NameWithoutArity(this Type type)
    {
        var name = type.Name;
        var arity = name.IndexOf('`');

        return arity < 0 ? name : name.Substring(0, arity);
    }

    /// <summary>
    /// String-keyed theory data. Unlike delegate-valued rows, strings serialize, so xUnit 2 discovers one test case
    /// per key rather than collapsing the theory into a single case.
    /// </summary>
    public static TheoryData<string> ToTheoryData(this IEnumerable<string> keys)
    {
        var data = new TheoryData<string>();
        foreach (var key in keys)
            data.Add(key);

        return data;
    }
}
