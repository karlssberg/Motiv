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
    private LeafScope(Type modelType, IReadOnlyDictionary<string, RuleParameterDeclaration> parameters, IReadOnlyDictionary<string, Type> variables)
    {
        ModelType = modelType;
        Parameters = parameters;
        Variables = variables;
    }

    public Type ModelType { get; }
    public IReadOnlyDictionary<string, RuleParameterDeclaration> Parameters { get; }
    public IReadOnlyDictionary<string, Type> Variables { get; }

    public static LeafScope For(Type modelType, IReadOnlyList<RuleParameterDeclaration> parameters) =>
        new(modelType, parameters.ToDictionary(p => p.Name, p => p, StringComparer.Ordinal), new Dictionary<string, Type>(StringComparer.Ordinal));

    public LeafScope WithVariable(string name, Type type)
    {
        var variables = new Dictionary<string, Type>(StringComparer.Ordinal);
        foreach (var kvp in Variables)
            variables[kvp.Key] = kvp.Value;
        variables[name] = type;
        return new LeafScope(ModelType, Parameters, variables);
    }

    /// <summary>
    /// The member a JSON name addresses: an explicit <see cref="JsonPropertyNameAttribute" /> wins,
    /// then the camel-cased CLR name (System.Text.Json's conventional policy), then the exact CLR
    /// name — so a host serializing with PascalCase still resolves.
    /// </summary>
    public static MemberInfo? FindMember(Type type, string jsonName)
    {
        var members = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Where(p => p.GetIndexParameters().Length == 0)
            .Cast<MemberInfo>()
            .Concat(type.GetFields(BindingFlags.Public | BindingFlags.Instance))
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

    public static Type MemberType(MemberInfo member) =>
        member is PropertyInfo property ? property.PropertyType : ((FieldInfo)member).FieldType;

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
