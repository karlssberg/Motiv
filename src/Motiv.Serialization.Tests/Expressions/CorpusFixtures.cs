#if NET8_0_OR_GREATER
using Motiv.Serialization.Expressions;

namespace Motiv.Serialization.Tests.Expressions;

/// <summary>
/// The two model shapes the conformance corpus (<c>ui/packages/rules-core/test/expression/corpus.json</c>)
/// checks against, and the type-name mapping ("int", "long", "decimal?", …) it compares corpus
/// case expectations to. Mirrors <c>ui/packages/rules-core/test/expression/fixture-schema.ts</c>
/// and the TypeScript checker's <c>typeString</c> in <c>check.ts</c> — both sides must agree on
/// every case in the corpus.
/// </summary>
public static class CorpusFixtures
{
    public sealed record Order(string Status, decimal Total, int DaysSinceShipped);

    public sealed record Customer(
        int Age, long Points, double Score, bool IsActive, decimal CreditLimit,
        string? Country, IReadOnlyList<Order>? Orders, DateTime? ShippedAt);

    /// <summary>
    /// The type string a corpus case's `type`/`result` expects: `int`, `long`, `decimal`, `bool`,
    /// `string`, … with a `?` suffix only for a nullable value type — a reference type (string,
    /// object, collection) is always nullable and never carries the suffix. Mirrors the private
    /// <see cref="LeafBinding" />.DescribeType and the TypeScript `typeString`.
    /// </summary>
    public static string TypeName(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        var name = underlying == typeof(int) ? "int"
            : underlying == typeof(long) ? "long"
            : underlying == typeof(decimal) ? "decimal"
            : underlying == typeof(double) ? "double"
            : underlying == typeof(float) ? "float"
            : underlying == typeof(bool) ? "bool"
            : underlying == typeof(string) ? "string"
            : underlying.Name;
        return underlying == type ? name : $"{name}?";
    }
}
#endif
