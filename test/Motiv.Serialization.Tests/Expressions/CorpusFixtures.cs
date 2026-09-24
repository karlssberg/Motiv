#if NET8_0_OR_GREATER && !MOTIV_NETSTANDARD_ASSET
using System.Text.Json.Serialization;
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
    /// <summary>Serialized by name, so a leaf compares it as a string — mirrors the fixture schema's
    /// <c>kind: { type: 'string', enum: [...] }</c>.</summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum OrderKind { Retail, Wholesale }

    public sealed record Order(string Status, decimal Total, int DaysSinceShipped, OrderKind Kind);

    public sealed record Customer(
        int Age, long Points, double Score, bool IsActive, decimal CreditLimit,
        string? Country, IReadOnlyList<Order>? Orders, DateTime? ShippedAt);

    /// <summary>The order a corpus case with <c>"scope": "order"</c> is evaluated against.</summary>
    public static readonly Order SampleOrder = new("paid", 620m, 12, OrderKind.Retail);

    /// <summary>The customer a corpus case with <c>"scope": "customer"</c> is evaluated against.</summary>
    public static readonly Customer SampleCustomer = new(
        34, 5_000L, 12.5d, true, 800m, "SE",
        [SampleOrder, new("pending", 950m, 0, OrderKind.Wholesale)], new DateTime(2026, 1, 1));

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
            : LeafScope.ElementType(underlying) is not null ? "collection" : "object";
        return underlying == type ? name : $"{name}?";
    }
}
#endif
