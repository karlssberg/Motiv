namespace Motiv.Tests.GeneratedBuilders;

/// <summary>
/// Shared inputs for the tests that pin how each Converj-generated builder method passes its
/// arguments along. Each test gives the true outcome a <c>"T"</c> / <see cref="Marker.True"/> payload
/// and the false outcome a <c>"F"</c> / <see cref="Marker.False"/> payload, so a generated method that
/// hands one outcome's payload to the other fails on <c>Values</c>.
/// </summary>
internal static class GeneratedBuilderFixtures
{
    public static readonly PolicyBase<int, string> IsPositivePolicy =
        Spec.Build((int n) => n > 0).Create("is positive");

    public static readonly SpecBase<int, string> IsPositiveSpec =
        Spec.Build((int n) => n > 0)
            .WhenTrueYield(_ => ["is positive"])
            .WhenFalseYield(_ => ["is not positive"])
            .Create("is positive");
}

/// <summary>
/// Non-string metadata, so the metadata overloads are chosen over the string ones. A class rather than
/// a record because records need <c>IsExternalInit</c>, which net472 does not have.
/// </summary>
internal sealed class Marker
{
    public static readonly Marker True = new("T");
    public static readonly Marker False = new("F");

    private readonly string _name;

    private Marker(string name) => _name = name;

    public override string ToString() => _name;
}
