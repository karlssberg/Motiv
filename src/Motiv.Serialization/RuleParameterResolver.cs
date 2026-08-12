using System.Reflection;

namespace Motiv.Serialization;

internal static class RuleParameterResolver
{
    public static IReadOnlyDictionary<string, object?>? ToDictionary(object? parameters)
    {
        switch (parameters)
        {
            case null:
                return null;
            case IReadOnlyDictionary<string, object?> dictionary:
                return dictionary;
            default:
                var values = new Dictionary<string, object?>(StringComparer.Ordinal);
                foreach (var property in parameters.GetType()
                             .GetProperties(BindingFlags.Public | BindingFlags.Instance))
                {
                    if (property.GetIndexParameters().Length != 0)
                        continue;

                    values[property.Name] = property.GetValue(parameters);
                }

                return values;
        }
    }

    /// <param name="errorPath">
    /// The JSON path errors are reported under. Defaults to the document's own <c>parameters</c>
    /// block; a parameterised spec node passes its own <c>args</c> path instead, so an argument
    /// error points at the node that supplied it rather than at a declaration block the document
    /// may not even have.
    /// </param>
    public static Dictionary<string, object?> Resolve(
        IReadOnlyList<RuleParameterDeclaration> declarations,
        IReadOnlyDictionary<string, object?>? supplied,
        List<RuleError> errors,
        string errorPath = "$.parameters")
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var declaration in declarations)
            values[declaration.Name] = ResolveValue(declaration, supplied, errors, errorPath);

        if (supplied is null)
            return values;

        var surplus = supplied.Keys.Where(name => declarations.All(declaration => declaration.Name != name));
        foreach (var name in surplus)
            errors.Add(new RuleError($"{errorPath}.{name}", RuleErrorCode.SurplusParameter,
                $"no parameter named '{name}' is declared"));

        return values;
    }

    public static Dictionary<string, object?> ResolveForValidation(
        IReadOnlyList<RuleParameterDeclaration> declarations)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var declaration in declarations)
            values[declaration.Name] = declaration.HasDefault ? declaration.DefaultValue : Placeholder(declaration);

        return values;
    }

    private static object? ResolveValue(
        RuleParameterDeclaration declaration,
        IReadOnlyDictionary<string, object?>? supplied,
        List<RuleError> errors,
        string errorPath)
    {
        if (supplied is not null && supplied.TryGetValue(declaration.Name, out var value))
            // A placeholder stands in on mismatch so interpolation does not cascade errors.
            return Coerce(declaration, value, errors, errorPath) ?? Placeholder(declaration);

        if (declaration.HasDefault)
            return declaration.DefaultValue;

        errors.Add(new RuleError($"{errorPath}.{declaration.Name}", RuleErrorCode.MissingParameter,
            $"the required parameter '{declaration.Name}' was not supplied"));
        return Placeholder(declaration);
    }

    private static object? Coerce(
        RuleParameterDeclaration declaration,
        object? value,
        List<RuleError> errors,
        string errorPath)
    {
        var coerced = Coerce(declaration.Type, value);

        if (coerced is null)
            errors.Add(new RuleError($"{errorPath}.{declaration.Name}", RuleErrorCode.ParameterTypeMismatch,
                $"the supplied value for '{declaration.Name}' does not match the declared type " +
                $"'{declaration.Type.ToString().ToLowerInvariant()}'"));

        return coerced;
    }

    /// <summary>
    /// The one coercion matrix, shared by document-supplied values, spec-node arguments and the
    /// declared defaults <see cref="RuleParameterDeclaration" /> normalizes at construction — so a
    /// default and a supplied value of the same CLR type can never disagree about what is legal.
    /// </summary>
    /// <returns>The value as its declared CLR type, or <c>null</c> when it does not match.</returns>
    internal static object? Coerce(RuleParameterType type, object? value) =>
        (type, value) switch
        {
            (RuleParameterType.Integer, int integer) => integer,
            (RuleParameterType.Integer, long l) when l is >= int.MinValue and <= int.MaxValue => (int)l,
            (RuleParameterType.Number, double d) => d,
            (RuleParameterType.Number, float f) => (double)f,
            (RuleParameterType.Number, int integer) => (double)integer,
            (RuleParameterType.Number, long l) => (double)l,
            (RuleParameterType.Number, decimal m) => (double)m,
            (RuleParameterType.String, string s) => s,
            (RuleParameterType.Boolean, bool b) => b,
            _ => null
        };

    private static object Placeholder(RuleParameterDeclaration declaration) =>
        declaration.Type switch
        {
            RuleParameterType.Integer => 0,
            RuleParameterType.Number => 0d,
            RuleParameterType.Boolean => false,
            _ => declaration.Name
        };
}
