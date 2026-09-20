using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Motiv.Serialization;

/// <summary>
/// Prints a rule document as the C# builder chain that compiles to the same specification:
/// definitions as locals, parameters as arguments, references through handles or the registry,
/// higher-order nodes as exactly the chain the binder builds. Every place the print cannot be
/// exact — an expression leaf, an object payload, a collection with no handle — is a warning, and
/// a <c>TODO</c> in the source beside the code it concerns.
/// </summary>
public static class CSharpPrinter
{
    /// <summary>Prints a document.</summary>
    /// <exception cref="RuleSerializationException">The document does not parse.</exception>
    public static CSharpPrintedRule Print(string documentJson, CSharpPrintOptions options)
    {
        if (documentJson is null) throw new ArgumentNullException(nameof(documentJson));
        if (options is null) throw new ArgumentNullException(nameof(options));

        var errors = new List<RuleError>();
        var document = new RuleDocumentParser(options.SerializerOptions ?? new RuleSerializerOptions()).Parse(documentJson, errors);
        if (document is null || errors.Count > 0)
            throw new RuleSerializationException(errors);

        return Print(document, options);
    }

    internal static CSharpPrintedRule Print(RuleDocument document, CSharpPrintOptions options) =>
        new Emitter(document, options).Emit();

    private sealed class Emitter(RuleDocument document, CSharpPrintOptions options)
    {
        private const string Indent = "    ";

        private readonly List<string> _warnings = [];
        private readonly Dictionary<string, string> _locals = new(StringComparer.Ordinal);
        private readonly HashSet<string> _taken = new(StringComparer.Ordinal);
        private readonly Stack<string> _models = new();
        private bool _usesDictionary;
        private bool _isAsync;

        /// <summary>The model type the node being printed is over: the rule's, or a collection's element inside a higher-order node.</summary>
        private string Model => _models.Count == 0 ? options.ModelType.Name : _models.Peek();

        public CSharpPrintedRule Emit()
        {
            // Every local is named before any body is printed, so a definition may reference one
            // declared after it in the document — the binder resolves them by name, not by order.
            foreach (var definition in document.Definitions)
                _locals[definition.Name!] = Identifier(definition.Name!);

            var body = new StringBuilder();
            foreach (var definition in document.Definitions)
                body.Append(Indent).Append("var ").Append(_locals[definition.Name!]).Append(" = ").Append(Node(definition)).Append(";\n");

            var root = Node(document.Root!);
            var returned = document.Name is null ? root : $"Spec.Build({root}).Create({Literal(document.Name)})";
            body.Append(Indent).Append("return ").Append(returned).Append(";\n");

            var method = new StringBuilder()
                .Append("public static ").Append(_isAsync ? "AsyncSpecBase" : "SpecBase").Append('<').Append(options.ModelType.Name).Append(", string> ")
                .Append(options.MethodName).Append("(SpecRegistry ").Append(options.RegistryExpression).Append(Parameters()).Append(")\n")
                .Append("{\n").Append(body).Append("}\n");

            return new CSharpPrintedRule(options.ClassName is null ? method.ToString() : Wrap(method.ToString()), _warnings);
        }

        /// <summary>Required parameters first, then defaulted — C# insists — each group in declaration order.</summary>
        private string Parameters()
        {
            var ordered = document.Parameters.Where(p => !p.HasDefault).Concat(document.Parameters.Where(p => p.HasDefault));
            var text = new StringBuilder();
            foreach (var parameter in ordered)
            {
                text.Append(", ").Append(TypeOf(parameter.Type)).Append(' ').Append(Identifier(parameter.Name, register: false));
                if (parameter.HasDefault)
                    text.Append(" = ").Append(Scalar(parameter.DefaultValue));
            }

            return text.ToString();
        }

        private string Wrap(string method)
        {
            var source = new StringBuilder();
            if (_usesDictionary)
                source.Append("using System.Collections.Generic;\n");
            source.Append("using Motiv;\nusing Motiv.Serialization;\n");
            if (!string.IsNullOrEmpty(options.ModelType.Namespace) && options.ModelType.Namespace != options.Namespace)
                source.Append("using ").Append(options.ModelType.Namespace).Append(";\n");
            source.Append('\n');
            if (options.Namespace is not null)
                source.Append("namespace ").Append(options.Namespace).Append(";\n\n");
            source.Append("public static class ").Append(options.ClassName).Append("\n{\n");
            foreach (var line in method.TrimEnd('\n').Split('\n'))
                source.Append(line.Length == 0 ? "" : Indent).Append(line).Append('\n');
            source.Append("}\n");
            return source.ToString();
        }

        /// <summary>The whole node: its operator, then whatever decoration it carries.</summary>
        private string Node(RuleNode node) => Decorate(node, Core(node));

        private string Core(RuleNode node) => node.Operator switch
        {
            RuleOperator.Spec => SpecReference(node),
            RuleOperator.Local => _locals[node.LocalName!],
            RuleOperator.Expression => ExpressionLeaf(node),
            RuleOperator.Not => "!" + Node(node.Children[0]),
            RuleOperator.And => Fold(node, (l, r) => $"({l} & {r})"),
            RuleOperator.Or => Fold(node, (l, r) => $"({l} | {r})"),
            RuleOperator.XOr => Fold(node, (l, r) => $"({l} ^ {r})"),
            RuleOperator.AndAlso => Fold(node, (l, r) => $"{l}.AndAlso({r})"),
            RuleOperator.OrElse => Fold(node, (l, r) => $"{l}.OrElse({r})"),
            _ => HigherOrder(node),
        };

        private string Fold(RuleNode node, Func<string, string, string> combine)
        {
            var result = Node(node.Children[0]);
            for (var index = 1; index < node.Children.Count; index++)
                result = combine(result, Node(node.Children[index]));
            return result;
        }

        private string SpecReference(RuleNode node)
        {
            var name = node.SpecName!;
            if (options.SpecHandles.TryGetValue(name, out var handle))
                return handle;

            var async = options.AsyncSpecs.Contains(name);
            _isAsync |= async;
            var call = new StringBuilder(options.RegistryExpression)
                .Append(async ? ".GetAsync<" : ".Get<").Append(Model).Append(">(").Append(Literal(name));
            if (node.Args is not null)
            {
                _usesDictionary = true;
                call.Append(", new Dictionary<string, object?> { ")
                    .Append(string.Join(", ", node.Args.Select(pair => $"[{Literal(pair.Key)}] = {Scalar(pair.Value)}")))
                    .Append(" }");
            }

            return call.Append(')').ToString();
        }

        private string ExpressionLeaf(RuleNode node)
        {
            _warnings.Add($"{node.Path}: expression leaf printed verbatim and not re-parsed; check it compiles as a lambda over {Model}");
            return $"Spec.From(({Model} m) => {node.ExpressionText}) /* expression printed verbatim; not re-parsed */";
        }

        /// <summary>The chain <c>HigherOrder.Build</c> makes, reanchored onto the model through the collection's selector.</summary>
        private string HigherOrder(RuleNode node)
        {
            var path = node.PathText!;
            options.Collections.TryGetValue(path, out var handle);
            var elementType = handle?.ElementType ?? "object";
            var selector = handle?.Selector ?? $"m => m.{CSharpIdentifiers.PascalCase(path)} /* TODO: the collection registered at '{path}' */";
            if (handle is null)
                _warnings.Add($"{node.Path}: no handle for the collection at '{path}'; the element type and selector are placeholders");
            else if (handle.Selector is null)
                _warnings.Add($"{node.Path}: no selector for the collection at '{path}'; the selector is a placeholder");

            _models.Push(elementType);
            var inner = Node(node.Children[0]);
            _models.Pop();

            var n = node.NParameterName is { } parameter ? Identifier(parameter, register: false) : node.N?.ToString(CultureInfo.InvariantCulture);
            var (quantifier, whenTrue, whenFalse) = node.Operator switch
            {
                RuleOperator.AsAllSatisfied => ("AsAllSatisfied()", "all satisfied", "not all satisfied"),
                RuleOperator.AsAnySatisfied => ("AsAnySatisfied()", "any satisfied", "none satisfied"),
                RuleOperator.AsNSatisfied => ($"AsNSatisfied({n})", $"exactly {{{n}}} satisfied", $"not exactly {{{n}}} satisfied"),
                RuleOperator.AsAtLeastNSatisfied => ($"AsAtLeastNSatisfied({n})", $"at least {{{n}}} satisfied", $"fewer than {{{n}}} satisfied"),
                _ => ($"AsAtMostNSatisfied({n})", $"at most {{{n}}} satisfied", $"more than {{{n}}} satisfied"),
            };

            return $"Spec.Build({inner}).{quantifier}.WhenTrue({Text(whenTrue)}).WhenFalse({Text(whenFalse)}).Create().ChangeModelTo<{Model}>({selector})";
        }

        private string Decorate(RuleNode node, string core)
        {
            if (node.HasObjectPayloads)
            {
                _warnings.Add($"{node.Path}: object whenTrue/whenFalse payloads printed as strings; the printed rule is an explanation rule");
                var chain = $"Spec.Build({core}).WhenTrue({Literal(JsonSerializer.Serialize(node.WhenTrueElement!.Value))}).WhenFalse({Literal(JsonSerializer.Serialize(node.WhenFalseElement!.Value))})";
                return $"{chain}.Create({(node.Name is null ? "" : Literal(node.Name))}) /* TODO: object payloads printed as strings */";
            }

            if (node.WhenTrueText is not null)
            {
                var chain = $"Spec.Build({core}).WhenTrue({Text(node.WhenTrueText)}).WhenFalse({Text(node.WhenFalseText!)})";
                return node.Name is null ? $"{chain}.Create()" : $"{chain}.Create({Literal(node.Name)})";
            }

            return node.Name is null ? core : $"Spec.Build({core}).Create({Literal(node.Name)})";
        }

        /// <summary>
        /// A payload text as the C# that yields the same string: an interpolated literal when it
        /// carries a <c>{parameter}</c> or an escaped brace — the document's syntax is C#'s — and a
        /// plain literal otherwise.
        /// </summary>
        private static string Text(string text) =>
            text.IndexOf('{') >= 0 || text.IndexOf('}') >= 0 ? "$" + Literal(text) : Literal(text);

        private static string Literal(string text)
        {
            var literal = new StringBuilder("\"");
            foreach (var character in text)
            {
                literal.Append(character switch
                {
                    '"' => "\\\"",
                    '\\' => "\\\\",
                    '\n' => "\\n",
                    '\r' => "\\r",
                    '\t' => "\\t",
                    _ => character.ToString(),
                });
            }

            return literal.Append('"').ToString();
        }

        private static string Scalar(object? value) => value switch
        {
            null => "null",
            bool flag => flag ? "true" : "false",
            int integer => integer.ToString(CultureInfo.InvariantCulture),
            long wide => wide.ToString(CultureInfo.InvariantCulture) + "L",
            double number => number.ToString("R", CultureInfo.InvariantCulture) + "d",
            string text => Literal(text),
            _ => Literal(Convert.ToString(value, CultureInfo.InvariantCulture) ?? ""),
        };

        private static string TypeOf(RuleParameterType type) => type switch
        {
            RuleParameterType.Integer => "int",
            RuleParameterType.Number => "double",
            RuleParameterType.Boolean => "bool",
            _ => "string",
        };

        /// <summary>A C# identifier for a document name; registered ones are kept unique across the method.</summary>
        private string Identifier(string name, bool register = true)
        {
            var identifier = CSharpIdentifiers.CamelCase(name);
            if (!register)
                return identifier;

            var candidate = identifier;
            for (var suffix = 2; !_taken.Add(candidate); suffix++)
                candidate = identifier + suffix.ToString(CultureInfo.InvariantCulture);
            return candidate;
        }
    }
}

/// <summary>Turns the names a document uses into the identifiers C# accepts.</summary>
internal static class CSharpIdentifiers
{
    private static readonly HashSet<string> Keywords = new(StringComparer.Ordinal)
    {
        "abstract", "as", "base", "bool", "break", "byte", "case", "catch", "char", "checked", "class", "const",
        "continue", "decimal", "default", "delegate", "do", "double", "else", "enum", "event", "explicit", "extern",
        "false", "finally", "fixed", "float", "for", "foreach", "goto", "if", "implicit", "in", "int", "interface",
        "internal", "is", "lock", "long", "namespace", "new", "null", "object", "operator", "out", "override",
        "params", "private", "protected", "public", "readonly", "ref", "return", "sbyte", "sealed", "short",
        "sizeof", "stackalloc", "static", "string", "struct", "switch", "this", "throw", "true", "try", "typeof",
        "uint", "ulong", "unchecked", "unsafe", "ushort", "using", "virtual", "void", "volatile", "while",
    };

    /// <summary><c>is-active</c> → <c>isActive</c>; <c>class</c> → <c>@class</c>; a leading digit gains <c>_</c>.</summary>
    public static string CamelCase(string name)
    {
        var pascal = PascalCase(name);
        if (pascal.Length == 0)
            return "_";

        var camel = char.ToLowerInvariant(pascal[0]) + pascal.Substring(1);
        if (char.IsDigit(camel[0]))
            camel = "_" + camel;
        return Keywords.Contains(camel) ? "@" + camel : camel;
    }

    /// <summary><c>account-orders</c> → <c>AccountOrders</c>; anything that is not a letter or digit separates words.</summary>
    public static string PascalCase(string name)
    {
        var result = new StringBuilder(name.Length);
        var startOfWord = true;
        foreach (var character in name)
        {
            if (!char.IsLetterOrDigit(character))
            {
                startOfWord = true;
                continue;
            }

            result.Append(startOfWord ? char.ToUpperInvariant(character) : character);
            startOfWord = false;
        }

        return result.ToString();
    }
}
