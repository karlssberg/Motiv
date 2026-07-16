using System.Globalization;
using System.Text;

namespace Motiv.Serialization;

internal static class RuleParameterSubstituter
{
    public static void Apply(
        RuleNode node,
        IReadOnlyDictionary<string, object?> values,
        List<RuleError> errors)
    {
        node.WhenTrueText = Interpolate(node.WhenTrueText, $"{node.Path}.whenTrue", values, errors);
        node.WhenFalseText = Interpolate(node.WhenFalseText, $"{node.Path}.whenFalse", values, errors);
        ResolveN(node, values, errors);

        foreach (var child in node.Children)
            Apply(child, values, errors);
    }

    private static void ResolveN(
        RuleNode node,
        IReadOnlyDictionary<string, object?> values,
        List<RuleError> errors)
    {
        if (node.NParameterName is not { } parameterName)
            return;

        if (!values.TryGetValue(parameterName, out var value))
        {
            errors.Add(new RuleError($"{node.Path}.n", RuleErrorCode.UnknownParameterReference,
                $"unknown parameter '{parameterName}'"));
            return;
        }

        if (value is int n and >= 0)
            node.N = n;
        else
            errors.Add(new RuleError($"{node.Path}.n", RuleErrorCode.ParameterTypeMismatch,
                $"'n' requires a non-negative 'integer' parameter; '{parameterName}' does not qualify"));
    }

    private static string? Interpolate(
        string? text,
        string path,
        IReadOnlyDictionary<string, object?> values,
        List<RuleError> errors)
    {
        if (text is null || (text.IndexOf('{') < 0 && text.IndexOf('}') < 0))
            return text;

        var result = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '{' when i + 1 < text.Length && text[i + 1] == '{':
                    result.Append('{');
                    i++;
                    break;
                case '}' when i + 1 < text.Length && text[i + 1] == '}':
                    result.Append('}');
                    i++;
                    break;
                case '}':
                    errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                        "unmatched '}' in payload string; use '}}' to escape a literal brace"));
                    return null;
                case '{':
                    var end = text.IndexOf('}', i + 1);
                    if (end < 0)
                    {
                        errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                            "unmatched '{' in payload string; use '{{' to escape a literal brace"));
                        return null;
                    }

                    var name = text.Substring(i + 1, end - i - 1);
                    if (!values.TryGetValue(name, out var value))
                    {
                        errors.Add(new RuleError(path, RuleErrorCode.UnknownParameterReference,
                            $"unknown parameter '{name}'"));
                        return null;
                    }

                    result.Append(Format(value));
                    i = end;
                    break;
                default:
                    result.Append(text[i]);
                    break;
            }
        }

        var interpolated = result.ToString();
        if (!string.IsNullOrWhiteSpace(interpolated))
            return interpolated;

        errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
            "payload string must not be empty or whitespace after parameter interpolation"));
        return null;
    }

    private static string Format(object? value) =>
        value switch
        {
            bool boolean => boolean ? "true" : "false",
            IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
            _ => value?.ToString() ?? string.Empty
        };
}
