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

    public static Dictionary<string, object?> Resolve(
        IReadOnlyList<RuleParameterDeclaration> declarations,
        IReadOnlyDictionary<string, object?>? supplied,
        List<RuleError> errors)
    {
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var declaration in declarations)
            values[declaration.Name] = ResolveValue(declaration, supplied, errors);

        if (supplied is null)
            return values;

        var surplus = supplied.Keys.Where(name => declarations.All(declaration => declaration.Name != name));
        foreach (var name in surplus)
            errors.Add(new RuleError($"$.parameters.{name}", RuleErrorCode.SurplusParameter,
                $"no parameter named '{name}' is declared by the document"));

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
        List<RuleError> errors)
    {
        if (supplied is not null && supplied.TryGetValue(declaration.Name, out var value))
            // A placeholder stands in on mismatch so interpolation does not cascade errors.
            return Coerce(declaration, value, errors) ?? Placeholder(declaration);

        if (declaration.HasDefault)
            return declaration.DefaultValue;

        errors.Add(new RuleError($"$.parameters.{declaration.Name}", RuleErrorCode.MissingParameter,
            $"the required parameter '{declaration.Name}' was not supplied"));
        return Placeholder(declaration);
    }

    private static object? Coerce(
        RuleParameterDeclaration declaration,
        object? value,
        List<RuleError> errors)
    {
        object? coerced = (declaration.Type, value) switch
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

        if (coerced is null)
            errors.Add(new RuleError($"$.parameters.{declaration.Name}", RuleErrorCode.ParameterTypeMismatch,
                $"the supplied value for '{declaration.Name}' does not match the declared type " +
                $"'{declaration.Type.ToString().ToLowerInvariant()}'"));

        return coerced;
    }

    private static object Placeholder(RuleParameterDeclaration declaration) =>
        declaration.Type switch
        {
            RuleParameterType.Integer => 0,
            RuleParameterType.Number => 0d,
            RuleParameterType.Boolean => false,
            _ => declaration.Name
        };
}
