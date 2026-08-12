using System.Text.Json;

namespace Motiv.Serialization;

internal sealed class RuleDocumentParser(RuleSerializerOptions options)
{
    private int _nodeCount;
    private bool _tooLargeReported;

    public RuleDocument? Parse(string json, List<RuleError> errors)
    {
        JsonDocument document;
        try
        {
            // Binary-operator nesting costs 2 JSON levels per rule level, so the reader's depth
            // ceiling must be raised beyond STJ's default of 64 to admit any document that is
            // legal under MaxDocumentDepth. Clamped so extreme option values cannot overflow.
            var maxDepth = (int)Math.Min((long)options.MaxDocumentDepth * 2 + 4, int.MaxValue);
            var readerOptions = new JsonDocumentOptions { MaxDepth = maxDepth };
            document = JsonDocument.Parse(json, readerOptions);
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
        var parameters = new List<RuleParameterDeclaration>();

        foreach (var property in root.EnumerateObject())
        {
            switch (property.Name)
            {
                case "$schema":
                    if (property.Value.ValueKind != JsonValueKind.String)
                        errors.Add(new RuleError("$.$schema", RuleErrorCode.InvalidNode,
                            "'$schema' must be a string"));
                    break;
                case "name":
                    name = ReadNonEmptyString(property.Value, "$.name", errors);
                    break;
                case "parameters":
                    parameters = ParseParameterDeclarations(property.Value, errors);
                    break;
                case "rule":
                    hasRule = true;
                    rule = ParseNode(property.Value, "$.rule", depth: 1, errors);
                    ReportIfComposesTooDeeply(rule, errors);
                    break;
                default:
                    errors.Add(new RuleError($"$.{property.Name}", RuleErrorCode.InvalidNode,
                        $"unknown property '{property.Name}'"));
                    break;
            }
        }

        if (!hasRule)
            errors.Add(new RuleError("$", RuleErrorCode.InvalidNode, "missing required property 'rule'"));

        return new RuleDocument(name, rule, parameters);
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
        JsonElement? nElement = null;
        JsonElement? pathElement = null;
        JsonElement? argsElement = null;
        string? name = null;

        foreach (var property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "spec" or "expression" or "not" or "and" or "or" or "xor" or "andAlso" or "orElse"
                    or "asAllSatisfied" or "asAnySatisfied" or "asNSatisfied"
                    or "asAtLeastNSatisfied" or "asAtMostNSatisfied":
                    operators.Add(property);
                    break;
                case "n":
                    nElement = property.Value;
                    break;
                case "path":
                    pathElement = property.Value;
                    break;
                case "args":
                    argsElement = property.Value;
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
                    errors.Add(new RuleError($"{path}.{property.Name}", RuleErrorCode.InvalidNode,
                        $"unknown property '{property.Name}'"));
                    break;
            }
        }

        if (operators.Count != 1)
        {
            errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                "rule node must contain exactly one of 'spec', 'expression', 'not', 'and', 'or', 'xor', " +
                "'andAlso', 'orElse', 'asAllSatisfied', 'asAnySatisfied', 'asNSatisfied', " +
                "'asAtLeastNSatisfied' or 'asAtMostNSatisfied'"));
            ParsePayloads(node: null, whenTrue, whenFalse, path, errors);
            return null;
        }

        var node = ParseOperator(operators[0], path, depth, errors);
        ParsePayloads(node, whenTrue, whenFalse, path, errors);
        if (node is null)
            return null;

        ApplyHigherOrderProperties(node, nElement, pathElement, path, errors);
        ApplyArguments(node, argsElement, path, errors);
        node.Name = name;

        if (node.HasObjectPayloads && node.Name is null)
        {
            errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                "nodes with object 'whenTrue'/'whenFalse' payloads must also declare a 'name'"));
            return null;
        }

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
            case "asAllSatisfied" or "asAnySatisfied" or "asNSatisfied"
                or "asAtLeastNSatisfied" or "asAtMostNSatisfied":
            {
                var @operator = property.Name switch
                {
                    "asAllSatisfied" => RuleOperator.AsAllSatisfied,
                    "asAnySatisfied" => RuleOperator.AsAnySatisfied,
                    "asNSatisfied" => RuleOperator.AsNSatisfied,
                    "asAtLeastNSatisfied" => RuleOperator.AsAtLeastNSatisfied,
                    _ => RuleOperator.AsAtMostNSatisfied
                };

                var child = ParseNode(property.Value, $"{path}.{property.Name}", depth + 1, errors);
                if (child is null)
                    return null;

                var node = new RuleNode(@operator, path);
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
        RuleNode? node,
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

        if (node is null)
            return;

        if (trueKind == JsonValueKind.String)
        {
            node.WhenTrueText = whenTrue.Value.GetString();
            node.WhenFalseText = whenFalse.Value.GetString();
        }
        else
        {
            node.WhenTrueElement = whenTrue.Value.Clone();
            node.WhenFalseElement = whenFalse.Value.Clone();
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

    private static List<RuleParameterDeclaration> ParseParameterDeclarations(
        JsonElement element,
        List<RuleError> errors)
    {
        var declarations = new List<RuleParameterDeclaration>();
        if (element.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new RuleError("$.parameters", RuleErrorCode.InvalidNode,
                "'parameters' must be a JSON object"));
            return declarations;
        }

        var seenNames = new HashSet<string>(StringComparer.Ordinal);
        foreach (var parameter in element.EnumerateObject())
        {
            if (!seenNames.Add(parameter.Name))
            {
                errors.Add(new RuleError($"$.parameters.{parameter.Name}", RuleErrorCode.InvalidNode,
                    $"duplicate parameter declaration '{parameter.Name}'"));
                continue;
            }

            var declaration = ParseParameterDeclaration(parameter, errors);
            if (declaration is not null)
                declarations.Add(declaration);
        }

        return declarations;
    }

    private static RuleParameterDeclaration? ParseParameterDeclaration(
        JsonProperty parameter,
        List<RuleError> errors)
    {
        var path = $"$.parameters.{parameter.Name}";
        if (parameter.Value.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                "parameter declaration must be a JSON object"));
            return null;
        }

        string? typeName = null;
        JsonElement? defaultElement = null;
        foreach (var property in parameter.Value.EnumerateObject())
        {
            switch (property.Name)
            {
                case "type":
                    typeName = property.Value.ValueKind == JsonValueKind.String
                        ? property.Value.GetString()
                        : null;
                    break;
                case "default":
                    defaultElement = property.Value;
                    break;
                default:
                    errors.Add(new RuleError($"{path}.{property.Name}", RuleErrorCode.InvalidNode,
                        $"unknown property '{property.Name}'"));
                    break;
            }
        }

        RuleParameterType? type = typeName switch
        {
            "integer" => RuleParameterType.Integer,
            "number" => RuleParameterType.Number,
            "string" => RuleParameterType.String,
            "boolean" => RuleParameterType.Boolean,
            _ => null
        };
        if (type is null)
        {
            errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                "parameter declaration must declare a 'type' of 'integer', 'number', 'string' or 'boolean'"));
            return null;
        }

        if (defaultElement is null)
            return new RuleParameterDeclaration(parameter.Name, type.Value, hasDefault: false, defaultValue: null);

        var defaultValue = ParseDefault(type.Value, defaultElement.Value, $"{path}.default", errors);
        return defaultValue is null
            ? null
            : new RuleParameterDeclaration(parameter.Name, type.Value, hasDefault: true, defaultValue);
    }

    private static object? ParseDefault(
        RuleParameterType type,
        JsonElement element,
        string path,
        List<RuleError> errors)
    {
        switch (type)
        {
            case RuleParameterType.Integer when element.ValueKind == JsonValueKind.Number:
                if (element.TryGetInt32(out var integer))
                    return integer;
                errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                    "integer parameter default must fit in a 32-bit integer"));
                return null;
            case RuleParameterType.Number when element.ValueKind == JsonValueKind.Number:
                return element.GetDouble();
            case RuleParameterType.String when element.ValueKind == JsonValueKind.String:
                return element.GetString();
            case RuleParameterType.Boolean when element.ValueKind is JsonValueKind.True or JsonValueKind.False:
                return element.GetBoolean();
            default:
                errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                    $"parameter default must match the declared type '{type.ToString().ToLowerInvariant()}'"));
                return null;
        }
    }

    private static void ApplyHigherOrderProperties(
        RuleNode node,
        JsonElement? nElement,
        JsonElement? pathElement,
        string path,
        List<RuleError> errors)
    {
        if (nElement is { } n)
        {
            if (node.Operator.RequiresN())
                ParseN(n, node, $"{path}.n", errors);
            else
                errors.Add(new RuleError($"{path}.n", RuleErrorCode.InvalidNode,
                    "'n' is only valid on 'asNSatisfied', 'asAtLeastNSatisfied' and 'asAtMostNSatisfied' nodes"));
        }
        else if (node.Operator.RequiresN())
        {
            errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                "this node requires 'n' (a non-negative integer or a '@parameter' reference)"));
        }

        if (pathElement is { } pathValue)
        {
            if (node.Operator.IsHigherOrder())
                node.PathText = ReadNonEmptyString(pathValue, $"{path}.path", errors);
            else
                errors.Add(new RuleError($"{path}.path", RuleErrorCode.InvalidNode,
                    "'path' is only valid on higher-order nodes"));
        }
        else if (node.Operator.IsHigherOrder())
        {
            errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                "higher-order nodes require a 'path' to the collection"));
        }
    }

    /// <summary>
    /// Reads a spec node's optional <c>args</c>: the scalar values a parameterised registry entry is
    /// built from. Only the values' JSON kinds are checked here — whether they satisfy the entry's
    /// declarations is a binding-time question, because only the registry knows what was declared.
    /// </summary>
    private static void ApplyArguments(
        RuleNode node,
        JsonElement? argsElement,
        string path,
        List<RuleError> errors)
    {
        if (argsElement is not { } args)
            return;

        if (node.Operator != RuleOperator.Spec)
        {
            errors.Add(new RuleError($"{path}.args", RuleErrorCode.InvalidNode,
                "'args' is only valid on 'spec' nodes"));
            return;
        }

        if (args.ValueKind != JsonValueKind.Object)
        {
            errors.Add(new RuleError($"{path}.args", RuleErrorCode.InvalidNode,
                "'args' must be a JSON object of scalar values"));
            return;
        }

        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var argument in args.EnumerateObject())
        {
            if (!TryReadScalar(argument.Value, out var value))
            {
                errors.Add(new RuleError($"{path}.args.{argument.Name}", RuleErrorCode.ParameterTypeMismatch,
                    $"the argument '{argument.Name}' must be a string, number, boolean or null"));
                continue;
            }

            values[argument.Name] = value;
        }

        node.Args = values;
    }

    private static bool TryReadScalar(JsonElement element, out object? value)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.String:
                value = element.GetString();
                return true;
            case JsonValueKind.Number when element.TryGetInt32(out var integer):
                value = integer;
                return true;
            case JsonValueKind.Number when element.TryGetInt64(out var wide):
                // Out of int range: kept as a long so the resolver reports the mismatch against an
                // 'integer' declaration, rather than the parser silently widening it to a double.
                value = wide;
                return true;
            case JsonValueKind.Number:
                value = element.GetDouble();
                return true;
            case JsonValueKind.True or JsonValueKind.False:
                value = element.GetBoolean();
                return true;
            case JsonValueKind.Null:
                value = null;
                return true;
            default:
                value = null;
                return false;
        }
    }

    private static void ParseN(JsonElement element, RuleNode node, string path, List<RuleError> errors)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Number when element.TryGetInt32(out var n) && n >= 0:
                node.N = n;
                return;
            case JsonValueKind.String when IsParameterReference(element.GetString()):
                node.NParameterName = element.GetString()!.Substring(1);
                return;
            default:
                errors.Add(new RuleError(path, RuleErrorCode.InvalidNode,
                    "'n' must be a non-negative integer or a '@parameter' reference"));
                return;
        }
    }

    private static bool IsParameterReference(string? text)
    {
        if (text is null || text.Length < 2 || text[0] != '@')
            return false;

        return IsIdentifierStart(text[1]) && text.Skip(2).All(IsIdentifierPart);

        static bool IsIdentifierStart(char ch) => ch is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or '_';
        static bool IsIdentifierPart(char ch) => IsIdentifierStart(ch) || ch is >= '0' and <= '9';
    }

    /// <summary>
    /// Refuses a rule whose <em>composed</em> spec tree would be deeper than
    /// <see cref="RuleSerializerOptions.MaxCompositionDepth" />.
    /// </summary>
    private void ReportIfComposesTooDeeply(RuleNode? rule, List<RuleError> errors)
    {
        if (rule is null || CompositionDepthOf(rule) <= options.MaxCompositionDepth)
            return;

        ReportTooLarge("$.rule",
            $"document composes deeper than the maximum composition depth of {options.MaxCompositionDepth}",
            errors);
    }

    /// <summary>
    /// The depth of the spec tree a node binds to, which is what result-tree walks recurse over —
    /// not the document's JSON nesting.
    /// </summary>
    /// <remarks>
    /// <see cref="RuleBinder" /> folds an n-ary operator left-deep (<c>((c₁ op c₂) op c₃) …</c>), so
    /// every operand after the first adds a level, and a nested operand's own depth compounds rather
    /// than adds — three operands nested three levels compose six deep, not three. That is why a
    /// per-node operand cap cannot bound this and <see cref="RuleSerializerOptions.MaxDocumentDepth" />
    /// does not either. Recursion here is bounded by <c>MaxDocumentDepth</c>, which is already
    /// enforced during parsing.
    /// </remarks>
    private static int CompositionDepthOf(RuleNode node)
    {
        // A leaf binds to a single spec, composing nothing.
        if (node.Children.Count == 0)
            return 0;

        var depth = CompositionDepthOf(node.Children[0]);

        // A single-operand node — 'not', and the higher-order quantifiers — still wraps its operand
        // in one composition level, which the fold below has no second operand to accumulate.
        if (node.Children.Count == 1)
            return depth + 1;

        // The left-deep fold: every operand after the first adds a level over the deepest so far.
        for (var index = 1; index < node.Children.Count; index++)
            depth = 1 + Math.Max(depth, CompositionDepthOf(node.Children[index]));

        return depth;
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
