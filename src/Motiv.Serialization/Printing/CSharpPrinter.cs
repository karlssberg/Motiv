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
        private readonly Dictionary<string, RuleParameterDeclaration> _parameters = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _arguments = new(StringComparer.Ordinal);
        private readonly HashSet<string> _taken = new(StringComparer.Ordinal);
        private bool _usesDictionary;
        private bool _isAsync;

        /// <summary>The model type the node being printed is over: the rule's, or a collection's element inside a higher-order node.</summary>
        private string _model = options.ModelType.Name;

        public CSharpPrintedRule Emit()
        {
            // Parameters are named first, in declaration order, so a hole in a payload text and the
            // argument it fills from agree; then every local, so a body may reference any of them.
            foreach (var parameter in document.Parameters)
            {
                _parameters[parameter.Name] = parameter;
                _arguments[parameter.Name] = UniqueIdentifier(parameter.Name);
            }

            foreach (var definition in document.Definitions)
                _locals[definition.Name!] = UniqueIdentifier(definition.Name!);

            // Only the definitions referenced at the rule's model become locals — a definition used
            // solely under a quantifier is bound over the element type, where it is printed inline,
            // and one used nowhere is never bound. Each local is declared before any local that
            // references it: the document's order is whatever the author wrote.
            var body = new StringBuilder();
            foreach (var definition in InDependencyOrder(ReferencedAtModel()))
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
            var text = new StringBuilder();
            foreach (var parameter in document.Parameters.OrderBy(p => p.HasDefault))
            {
                text.Append(", ").Append(TypeOf(parameter.Type)).Append(' ').Append(_arguments[parameter.Name]);
                if (parameter.HasDefault)
                    text.Append(" = ").Append(Scalar(parameter.DefaultValue));
            }

            return text.ToString();
        }

        /// <summary>
        /// The definitions the root reaches at the rule's model, directly or through other such
        /// definitions, in declaration order. A reference under a quantifier is over the element
        /// type and does not count; the binder binds that one at the reference, never as a local.
        /// </summary>
        private IReadOnlyList<RuleNode> ReferencedAtModel()
        {
            var byName = document.Definitions.ToDictionary(definition => definition.Name!, StringComparer.Ordinal);
            var referenced = new HashSet<string>(StringComparer.Ordinal);
            Walk(document.Root!);
            return document.Definitions.Where(definition => referenced.Contains(definition.Name!)).ToList();

            void Walk(RuleNode node)
            {
                if (node.Operator.IsHigherOrder())
                    return;
                if (node.Operator == RuleOperator.Local)
                {
                    if (referenced.Add(node.LocalName!) && byName.TryGetValue(node.LocalName!, out var definition))
                        Walk(definition);
                    return;
                }

                foreach (var child in node.Children)
                    Walk(child);
            }
        }

        /// <summary>
        /// Definitions so that each follows every definition it references, declaration order as
        /// the tiebreak. The parser has already refused cycles, so the walk terminates.
        /// </summary>
        private static IEnumerable<RuleNode> InDependencyOrder(IReadOnlyList<RuleNode> definitions)
        {
            var byName = definitions.ToDictionary(definition => definition.Name!, StringComparer.Ordinal);
            var ordered = new List<RuleNode>();
            var visited = new HashSet<string>(StringComparer.Ordinal);

            foreach (var definition in definitions)
                Visit(definition);
            return ordered;

            void Visit(RuleNode definition)
            {
                if (!visited.Add(definition.Name!))
                    return;
                foreach (var referenced in ReferencedLocals(definition))
                {
                    if (byName.TryGetValue(referenced, out var dependency))
                        Visit(dependency);
                }

                ordered.Add(definition);
            }
        }

        private static IEnumerable<string> ReferencedLocals(RuleNode node)
        {
            if (node.Operator == RuleOperator.Local)
                yield return node.LocalName!;
            foreach (var child in node.Children)
            {
                foreach (var referenced in ReferencedLocals(child))
                    yield return referenced;
            }
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
            // A local is a name the binder binds at the reference's model. Over the rule's model that
            // is the declared local; under a quantifier it is the definition again, over the element.
            RuleOperator.Local => _model == options.ModelType.Name ? _locals[node.LocalName!] : Node(node.Definition!),
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
                .Append(async ? ".GetAsync<" : ".Get<").Append(_model).Append(">(").Append(Literal(name));
            if (node.Args is not null)
            {
                _usesDictionary = true;
                call.Append(", new Dictionary<string, object?> { ")
                    .Append(string.Join(", ", node.Args.Select(pair => $"[{Literal(pair.Key)}] = {Scalar(pair.Value)}")))
                    .Append(" }");
            }

            call.Append(')');
            if (options.KnownSpecs is { } known && !known.Contains(name))
            {
                _warnings.Add($"{node.Path}: '{name}' is not a compiled spec on this host; registry.Get will throw UnknownSpec until it is registered or the reference is replaced");
                call.Append(" /* TODO: '").Append(name).Append("' is not a compiled spec */");
            }

            return call.ToString();
        }

        private string ExpressionLeaf(RuleNode node)
        {
            _warnings.Add($"{node.Path}: expression leaf printed verbatim and not re-parsed; check it compiles as a lambda over {_model}");
            return $"Spec.From(({_model} m) => {node.ExpressionText}) /* expression printed verbatim; not re-parsed */";
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

            // The inner spec is over the element type; everything outside it is over the model again.
            var outerModel = _model;
            _model = elementType;
            var inner = Node(node.Children[0]);
            _model = outerModel;

            var n = node.NParameterName is { } parameter
                ? _arguments.TryGetValue(parameter, out var argument) ? argument : CSharpIdentifiers.CamelCase(parameter)
                : node.N?.ToString(CultureInfo.InvariantCulture);

            // The payloads name n the way the document does: a hole Text fills from the parameter,
            // or the literal count written straight in.
            var count = node.NParameterName is { } named ? "{" + named + "}" : n;
            var (quantifier, whenTrue, whenFalse) = node.Operator switch
            {
                RuleOperator.AsAllSatisfied => ("AsAllSatisfied()", "all satisfied", "not all satisfied"),
                RuleOperator.AsAnySatisfied => ("AsAnySatisfied()", "any satisfied", "none satisfied"),
                RuleOperator.AsNSatisfied => ($"AsNSatisfied({n})", $"exactly {count} satisfied", $"not exactly {count} satisfied"),
                RuleOperator.AsAtLeastNSatisfied => ($"AsAtLeastNSatisfied({n})", $"at least {count} satisfied", $"fewer than {count} satisfied"),
                _ => ($"AsAtMostNSatisfied({n})", $"at most {count} satisfied", $"more than {count} satisfied"),
            };

            return $"Spec.Build({inner}).{quantifier}.WhenTrue({Text(whenTrue)}).WhenFalse({Text(whenFalse)}).Create().ChangeModelTo<{_model}>({selector})";
        }

        private string Decorate(RuleNode node, string core)
        {
            // An unnamed node's Create() takes no argument; the name, when there is one, is its only one.
            var name = node.Name is null ? "" : Literal(node.Name);

            if (node.HasObjectPayloads)
            {
                _warnings.Add($"{node.Path}: object whenTrue/whenFalse payloads printed as strings; the printed rule is an explanation rule");
                var trueJson = Literal(JsonSerializer.Serialize(node.WhenTrueElement!.Value));
                var falseJson = Literal(JsonSerializer.Serialize(node.WhenFalseElement!.Value));
                return $"Spec.Build({core}).WhenTrue({trueJson}).WhenFalse({falseJson}).Create({name}) /* TODO: object payloads printed as strings */";
            }

            if (node.WhenTrueText is not null)
                return $"Spec.Build({core}).WhenTrue({Text(node.WhenTrueText)}).WhenFalse({Text(node.WhenFalseText!)}).Create({name})";

            return node.Name is null ? core : $"Spec.Build({core}).Create({name})";
        }

        /// <summary>
        /// A payload text as the C# that yields the string the substituter would: a plain literal
        /// when it has no brace, otherwise an interpolated literal whose holes name the printed
        /// arguments and format as <c>RuleParameterSubstituter</c> does — <c>true</c>/<c>false</c>
        /// for a boolean, the invariant culture for a number. A hole naming no parameter is kept as
        /// written; the document would not bind either.
        /// </summary>
        private string Text(string text)
        {
            if (text.IndexOf('{') < 0 && text.IndexOf('}') < 0)
                return Literal(text);

            var literal = new StringBuilder("$\"");
            for (var index = 0; index < text.Length; index++)
            {
                var character = text[index];
                if (character == '{' && index + 1 < text.Length && text[index + 1] == '{')
                {
                    literal.Append("{{");
                    index++;
                }
                else if (character == '}' && index + 1 < text.Length && text[index + 1] == '}')
                {
                    literal.Append("}}");
                    index++;
                }
                else if (character == '{' && text.IndexOf('}', index + 1) is var end && end > index)
                {
                    literal.Append('{').Append(Hole(text.Substring(index + 1, end - index - 1))).Append('}');
                    index = end;
                }
                else
                {
                    literal.Append(Escaped(character));
                }
            }

            return literal.Append('"').ToString();
        }

        private string Hole(string name)
        {
            if (!_parameters.TryGetValue(name, out var parameter))
                return name;

            var argument = _arguments[name];
            return parameter.Type switch
            {
                RuleParameterType.Boolean => $"({argument} ? \"true\" : \"false\")",
                RuleParameterType.Number => $"{argument}.ToString(System.Globalization.CultureInfo.InvariantCulture)",
                _ => argument,
            };
        }

        private static string Literal(string text)
        {
            var literal = new StringBuilder("\"");
            foreach (var character in text)
                literal.Append(Escaped(character));
            return literal.Append('"').ToString();
        }

        private static string Escaped(char character) => character switch
        {
            '"' => "\\\"",
            '\\' => "\\\\",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            _ => character.ToString(),
        };

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

        /// <summary>A C# identifier for a document name, kept distinct from every other one this method declares.</summary>
        private string UniqueIdentifier(string name)
        {
            var identifier = CSharpIdentifiers.CamelCase(name);
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

    /// <summary>
    /// <c>2fa-check</c> → <c>_2faCheck</c>: <see cref="PascalCase"/>, made a legal type name — a
    /// leading digit gains <c>_</c>, and an empty result becomes <c>_</c>.
    /// </summary>
    public static string TypeName(string name)
    {
        var pascal = PascalCase(name);
        return pascal.Length == 0 || char.IsDigit(pascal[0]) ? "_" + pascal : pascal;
    }

    /// <summary>
    /// The C# name of a type as adopted code would write it: the keyword for a framework primitive
    /// (<c>int</c>, not <c>Int32</c>, which needs a <c>using System</c> the print does not emit),
    /// the plain name for anything else.
    /// </summary>
    public static string TypeName(Type type)
    {
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(short)) return "short";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(sbyte)) return "sbyte";
        if (type == typeof(uint)) return "uint";
        if (type == typeof(ulong)) return "ulong";
        if (type == typeof(ushort)) return "ushort";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(string)) return "string";
        if (type == typeof(char)) return "char";
        if (type == typeof(double)) return "double";
        if (type == typeof(float)) return "float";
        if (type == typeof(decimal)) return "decimal";
        if (type == typeof(object)) return "object";
        return type.Name;
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
