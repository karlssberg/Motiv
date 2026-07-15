using System.Text.Json;

namespace Motiv.Serialization;

internal sealed class RuleDocumentParser(RuleSerializerOptions options)
{
    private static readonly string[] HigherOrderProperties =
    [
        "asAllSatisfied", "asAnySatisfied", "asNSatisfied", "asAtLeastNSatisfied", "asAtMostNSatisfied", "n", "path"
    ];

    private static readonly string[] ParameterTypes = ["integer", "number", "string", "boolean"];

    private int _nodeCount;
    private bool _tooLargeReported;

    public RuleDocument? Parse(string json, List<RuleError> errors)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            errors.Add(new RuleError("$", RuleErrorCode.InvalidNode, $"invalid JSON: {exception.Message}"));
            return null;
        }

        using (document)
        {
            return ParseEnvelope(document.RootElement, errors);
        }
    }

    private RuleDocument? ParseEnvelope(JsonElement root, List<RuleError> errors)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new RuleError("$", RuleErrorCode.InvalidNode, "document must be a JSON object"));
            return null;
        }

        string? name = null;
        RuleNode? rule = null;
        var hasRule = false;

        foreach (var property in root.EnumerateObject())
        {
            switch (property.Name)
            {
                case "$schema":
                    break;
                case "name":
                    name = ReadNonEmptyString(property.Value, "$.name", errors);
                    break;
                case "parameters":
                    ValidateParameterDeclarations(property.Value, errors);
                    break;
                case "rule":
                    hasRule = true;
                    rule = ParseNode(property.Value, "$.rule", depth: 1, errors);
                    break;
                default:
                    errors.Add(new RuleError($"$.{property.Name}", RuleErrorCode.InvalidNode,
                        $"unknown property '{property.Name}'"));
                    break;
            }
        }

        if (!hasRule)
            errors.Add(new RuleError("$", RuleErrorCode.InvalidNode, "missing required property 'rule'"));

        return new RuleDocument(name, rule);
    }

    private RuleNode? ParseNode(JsonElement element, string path, int depth, List<RuleError> errors)
    {
        if (ExceedsLimits(path, depth, errors))
            return null;

        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new RuleError(path, RuleErrorCode.InvalidNode, "rule node must be a JSON object"));
            return null;
        }

        var operators = new List<JsonProperty>();
        JsonElement? whenTrue = null;
        JsonElement? whenFalse = null;
        string? name = null;

        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "spec" or "expression" or "not" or "and" or "or" or "xor" or "andAlso" or "orElse":
                    operators.Add(property);
                    break;
                case "whenTrue":
                    whenTrue = property.Value;
                    break;
                case "whenFalse":
                    whenFalse = property.Value;
                    break;
                case "name":
                    name = ReadNonEmptyString(property.Value, $"{path}.name", errors);
                    break;
                default:
                    var message = HigherOrderProperties.Contains(property.Name)
                        ? $"'{property.Name}' is part of the rule format but is not yet supported by this loader"
                        : $"unknown property '{property.Name}'";
                    errors.Add(new RuleError($"{path}.{property.Name}", RuleErrorCode.InvalidNode, message));
                    break;
            }
        }

        if (operators.Count != 1)
        {
            errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                "rule node must contain exactly one of 'spec', 'expression', 'not', 'and', 'or', 'xor', " +
                "'andAlso' or 'orElse'"));
            return null;
        }

        var node = ParseOperator(operators[0], path, depth, errors);
        if (node is null)
            return null;

        node.Name = name;
        ParsePayloads(node, whenTrue, whenFalse, path, errors);
        return node;
    }

    private RuleNode? ParseOperator(JsonProperty property, string path, int depth, List<RuleError> errors)
    {
        switch (property.Name)
        {
            case "spec":
            {
                var specName = ReadNonEmptyString(property.Value, $"{path}.spec", errors);
                return specName is null ? null : new RuleNode(RuleOperator.Spec, path) { SpecName = specName };
            }
            case "expression":
            {
                var expression = ReadNonEmptyString(property.Value, $"{path}.expression", errors);
                return expression is null
                    ? null
                    : new RuleNode(RuleOperator.Expression, path) { ExpressionText = expression };
            }
            case "not":
            {
                var child = ParseNode(property.Value, $"{path}.not", depth + 1, errors);
                if (child is null)
                    return null;

                var node = new RuleNode(RuleOperator.Not, path);
                node.Children.Add(child);
                return node;
            }
            default:
                return ParseBinaryOperator(property, path, depth, errors);
        }
    }

    private RuleNode? ParseBinaryOperator(JsonProperty property, string path, int depth, List<RuleError> errors)
    {
        var @operator = property.Name switch
        {
            "and" => RuleOperator.And,
            "or" => RuleOperator.Or,
            "xor" => RuleOperator.XOr,
            "andAlso" => RuleOperator.AndAlso,
            _ => RuleOperator.OrElse
        };

        if (property.Value.ValueKind != JsonValueKind.Array || property.Value.GetArrayLength() < 2)
        {
            errors.Add(new RuleError($"{path}.{property.Name}", RuleErrorCode.InvalidNode,
                $"'{property.Name}' must be an array of at least two rule nodes"));
            return null;
        }

        var node = new RuleNode(@operator, path);
        var index = 0;
        foreach (var item in property.Value.EnumerateArray())
        {
            var child = ParseNode(item, $"{path}.{property.Name}[{index}]", depth + 1, errors);
            if (child is not null)
                node.Children.Add(child);
            index++;
        }

        return node.Children.Count == index ? node : null;
    }

    private static void ParsePayloads(
        RuleNode node,
        JsonElement? whenTrue,
        JsonElement? whenFalse,
        string path,
        List<RuleError> errors)
    {
        if (whenTrue is null && whenFalse is null)
            return;

        if (whenTrue is null || whenFalse is null)
        {
            errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                "'whenTrue' and 'whenFalse' must be supplied together"));
            return;
        }

        var trueKind = ClassifyPayload(whenTrue.Value, $"{path}.whenTrue", errors);
        var falseKind = ClassifyPayload(whenFalse.Value, $"{path}.whenFalse", errors);
        if (trueKind is null || falseKind is null)
            return;

        if (trueKind != falseKind)
        {
            errors.Add(new RuleError(path, RuleErrorCode.MixedWhenTrueFalseKinds,
                "'whenTrue' and 'whenFalse' must be the same kind: both strings or both objects"));
            return;
        }

        if (trueKind == JsonValueKind.String)
        {
            node.WhenTrueText = whenTrue.Value.GetString();
            node.WhenFalseText = whenFalse.Value.GetString();
        }
        else
        {
            node.HasObjectPayloads = true;
        }
    }

    private static JsonValueKind? ClassifyPayload(JsonElement element, string path, List<RuleError> errors)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String when string.IsNullOrWhiteSpace(element.GetString()):
                errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                    "payload string must not be empty or whitespace"));
                return null;
            case JsonValueKind.String:
                return JsonValueKind.String;
            case JsonValueKind.Object:
                return JsonValueKind.Object;
            default:
                errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                    "'whenTrue'/'whenFalse' must be a string or a JSON object"));
                return null;
        }
    }

    private static void ValidateParameterDeclarations(JsonElement element, List<RuleError> errors)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new RuleError("$.parameters", RuleErrorCode.InvalidNode,
                "'parameters' must be a JSON object"));
            return;
        }

        foreach (var parameter in element.EnumerateObject())
        {
            var path = $"$.parameters.{parameter.Name}";
            if (parameter.Value.ValueKind != JsonValueKind.Object)
            {
                errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                    "parameter declaration must be a JSON object"));
                continue;
            }

            var hasType = false;
            foreach (var property in parameter.Value.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "type":
                        hasType = true;
                        if (property.Value.ValueKind != JsonValueKind.String ||
                            !ParameterTypes.Contains(property.Value.GetString()))
                        {
                            errors.Add(new RuleError($"{path}.type", RuleErrorCode.InvalidNode,
                                "parameter type must be one of 'integer', 'number', 'string' or 'boolean'"));
                        }

                        break;
                    case "default":
                        break;
                    default:
                        errors.Add(new RuleError($"{path}.{property.Name}", RuleErrorCode.InvalidNode,
                            $"unknown property '{property.Name}'"));
                        break;
                }
            }

            if (!hasType)
                errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                    "parameter declaration must declare a 'type'"));
        }
    }

    private bool ExceedsLimits(string path, int depth, List<RuleError> errors)
    {
        if (depth > options.MaxDocumentDepth)
            return ReportTooLarge(path, $"document exceeds the maximum depth of {options.MaxDocumentDepth}", errors);

        _nodeCount++;
        if (_nodeCount > options.MaxNodeCount)
            return ReportTooLarge(path, $"document exceeds the maximum node count of {options.MaxNodeCount}",
                errors);

        return false;
    }

    private bool ReportTooLarge(string path, string message, List<RuleError> errors)
    {
        if (!_tooLargeReported)
        {
            _tooLargeReported = true;
            errors.Add(new RuleError(path, RuleErrorCode.DocumentTooLarge, message));
        }

        return true;
    }

    private static string? ReadNonEmptyString(JsonElement element, string path, List<RuleError> errors)
    {
        if (element.ValueKind == JsonValueKind.String)
        {
            var value = element.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        errors.Add(new RuleError(path, RuleErrorCode.InvalidNode, "value must be a non-empty string"));
        return null;
    }
}
