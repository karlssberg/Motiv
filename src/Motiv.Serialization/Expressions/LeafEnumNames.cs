using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Motiv.Serialization.Expressions;

/// <summary>
/// The names an enum is written with when the host serializes it by name, read by serializing its
/// values rather than by recognising converters — so a converter registered on the options, one
/// carried as an attribute, a naming policy and a per-value name override all count exactly as the
/// catalog's schema (exported over the same options) counts them.
/// </summary>
internal sealed class LeafEnumNames
{
    private readonly Dictionary<object, string> _byValue;

    private LeafEnumNames(Type enumType, Dictionary<object, string> byValue)
    {
        _byValue = byValue;
        Names = byValue.Values.Distinct(StringComparer.Ordinal).ToArray();
        AreClrNames = byValue.All(kvp => Enum.GetName(enumType, kvp.Key) == kvp.Value);
    }

    /// <summary>The names a literal may compare against, in value order.</summary>
    public IReadOnlyList<string> Names { get; }

    /// <summary>Whether every value is written with its CLR name, so <c>ToString()</c> already reads it.</summary>
    public bool AreClrNames { get; }

    /// <summary>
    /// The name a value is written with. Called by compiled leaves, so it never serializes: a value
    /// with no defined name (a flags combination, an out-of-range number) can equal none of
    /// <see cref="Names" />, the only literals the checker accepts, so its <c>ToString()</c> serves.
    /// </summary>
    public string NameOf(object value) =>
        _byValue.TryGetValue(value, out var name) ? name : value.ToString()!;

    /// <summary>
    /// How <paramref name="member" />'s enum is written under <paramref name="json" />, or <c>null</c>
    /// when it is written as a number. A <see cref="JsonConverterAttribute" /> on the member outranks
    /// the options, as it does when System.Text.Json serializes the member itself.
    /// </summary>
    public static LeafEnumNames? For(MemberInfo member, Type enumType, JsonSerializerOptions json)
    {
        var options = WithMemberConverter(member, enumType, json);
        var byValue = new Dictionary<object, string>();
        foreach (var value in Enum.GetValues(enumType))
        {
            if (byValue.ContainsKey(value)) continue;
            if (WrittenName(value, enumType, options) is not { } name) return null;
            byValue[value] = name;
        }
        return byValue.Count == 0 ? null : new LeafEnumNames(enumType, byValue);
    }

    private static JsonSerializerOptions WithMemberConverter(MemberInfo member, Type enumType, JsonSerializerOptions json)
    {
        if (member.GetCustomAttribute<JsonConverterAttribute>() is not { } attribute)
            return json;
        var converter = attribute.CreateConverter(enumType)
            ?? (attribute.ConverterType is { } type ? Activator.CreateInstance(type) as JsonConverter : null);
        if (converter is null)
            return json;
        var options = new JsonSerializerOptions(json);
        options.Converters.Insert(0, converter);
        return options;
    }

    /// <summary>
    /// Empty, because a compiled leaf passes this instance to <see cref="LeafEnumNameExtensions.ToJsonName{TEnum}" />
    /// and its clause is printed from the expression tree: the call then reads <c>model.Priority.ToJsonName()</c>.
    /// </summary>
    public override string ToString() => string.Empty;

    private static string? WrittenName(object value, Type enumType, JsonSerializerOptions json)
    {
        var written = JsonSerializer.SerializeToElement(value, enumType, json);
        return written.ValueKind == JsonValueKind.String ? written.GetString() : null;
    }
}

/// <summary>The call a compiled leaf makes to read an enum by the name its JSON is written with.</summary>
internal static class LeafEnumNameExtensions
{
    public static string ToJsonName<TEnum>(this TEnum value, LeafEnumNames names) where TEnum : struct, Enum =>
        names.NameOf(value);
}
