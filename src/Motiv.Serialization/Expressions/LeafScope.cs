using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Motiv.Serialization.Expressions;

/// <summary>
/// What a name means at a point in a leaf: the model type, the lambda variables in scope, and
/// the document's parameters. Members are addressed by their JSON-schema names, because that is
/// what the catalog publishes and what the editor completes — never by CLR names.
/// </summary>
internal sealed class LeafScope
{
    private readonly JsonSerializerOptions _json;
    private readonly Dictionary<MemberInfo, LeafEnumNames?> _enumNames;

    private LeafScope(
        Type modelType,
        IReadOnlyDictionary<string, RuleParameterDeclaration> parameters,
        IReadOnlyDictionary<string, Type> variables,
        JsonSerializerOptions json,
        Dictionary<MemberInfo, LeafEnumNames?> enumNames)
    {
        ModelType = modelType;
        Parameters = parameters;
        Variables = variables;
        _json = json;
        _enumNames = enumNames;
    }

    public Type ModelType { get; }
    public IReadOnlyDictionary<string, RuleParameterDeclaration> Parameters { get; }
    public IReadOnlyDictionary<string, Type> Variables { get; }

    /// <param name="modelType">The type unqualified names resolve against.</param>
    /// <param name="parameters">The document's parameter declarations.</param>
    /// <param name="modelJson">
    /// The options the host serializes models with, deciding which enums read by name; <c>null</c>
    /// uses System.Text.Json's defaults, where only a <c>[JsonConverter]</c> attribute does.
    /// </param>
    public static LeafScope For(Type modelType, IReadOnlyList<RuleParameterDeclaration> parameters, JsonSerializerOptions? modelJson = null) =>
        new(modelType,
            parameters.ToDictionary(p => p.Name, p => p, StringComparer.Ordinal),
            new Dictionary<string, Type>(StringComparer.Ordinal),
            modelJson ?? JsonSerializerOptions.Default,
            new Dictionary<MemberInfo, LeafEnumNames?>());

    public LeafScope WithVariable(string name, Type type)
    {
        // No IEnumerable<KeyValuePair<,>> constructor on netstandard2.0, so copy through LINQ.
        var variables = Variables.ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.Ordinal);
        variables[name] = type;
        return new LeafScope(ModelType, Parameters, variables, _json, _enumNames);
    }

    /// <summary>
    /// The member a JSON name addresses: an explicit <see cref="JsonPropertyNameAttribute" /> wins,
    /// then the camel-cased CLR name (System.Text.Json's conventional policy), then the exact CLR
    /// name — so a host serializing with PascalCase still resolves. Only members the serializer
    /// would actually publish are addressable: non-public ones are never read, and one carrying
    /// <see cref="JsonIgnoreAttribute" /> is addressable only under <see cref="JsonIgnoreCondition.Never" />,
    /// the one condition that means "do not ignore".
    /// </summary>
    public static MemberInfo? FindMember(Type type, string jsonName)
    {
        var members = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Cast<MemberInfo>()
            .Concat(type.GetFields(BindingFlags.Public | BindingFlags.Instance))
            .Where(IsSerialized)
            .Select(member => (member, jsonPropertyName: member.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name))
            .ToList();

        foreach (var (member, jsonPropertyName) in members)
            if (jsonPropertyName == jsonName)
                return member;

        foreach (var (member, jsonPropertyName) in members)
            if (jsonPropertyName is null && JsonNamingPolicy.CamelCase.ConvertName(member.Name) == jsonName)
                return member;

        return members.FirstOrDefault(m => m.jsonPropertyName is null && m.member.Name == jsonName).member;
    }

    /// <summary>Whether System.Text.Json would publish this member at all.</summary>
    private static bool IsSerialized(MemberInfo member)
    {
        var ignore = member.GetCustomAttribute<JsonIgnoreAttribute>();
        return ignore is null || ignore.Condition == JsonIgnoreCondition.Never;
    }

    public static Type MemberType(MemberInfo member) =>
        member is PropertyInfo property ? property.PropertyType : ((FieldInfo)member).FieldType;

    /// <summary>The enum a type resolves to — through <see cref="Nullable{T}" /> — or null.</summary>
    public static Type? EnumType(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        return underlying.IsEnum ? underlying : null;
    }

    /// <summary>
    /// The names a member's enum is written with, or <c>null</c> when the member is no enum or is
    /// written as a number. Cached per member for the life of the scope and every scope derived from it.
    /// </summary>
    public LeafEnumNames? EnumNames(MemberInfo member)
    {
        if (_enumNames.TryGetValue(member, out var cached))
            return cached;
        var enumType = EnumType(MemberType(member));
        var names = enumType is null ? null : LeafEnumNames.For(member, enumType, _json);
        _enumNames[member] = names;
        return names;
    }

    /// <summary>
    /// The type a member presents to the leaf language: an enum serialized by name reads as a
    /// <see cref="string" />, any other enum as the integral kind the lattice compares by, and
    /// everything else as itself — so a leaf types against the JSON the catalog publishes rather
    /// than against the CLR member behind it.
    /// </summary>
    public Type LeafMemberType(MemberInfo member)
    {
        var declared = MemberType(member);
        var enumType = EnumType(declared);
        if (enumType is null) return declared;
        if (EnumNames(member) is not null) return typeof(string);
        var backing = Enum.GetUnderlyingType(enumType);
        var integral = backing == typeof(long) || backing == typeof(ulong) ? typeof(long) : typeof(int);
        return Nullable.GetUnderlyingType(declared) is null ? integral : typeof(Nullable<>).MakeGenericType(integral);
    }

    /// <summary>The element type of anything enumerable except <see cref="string" />; null otherwise.</summary>
    public static Type? ElementType(Type type)
    {
        if (type == typeof(string))
            return null;
        var enumerable = type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type
            : type.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        return enumerable?.GetGenericArguments()[0];
    }
}
