using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Motiv.CodeFix;

/// <summary>
///     The methods of the containing type that an expression calls. The spec is a class of its own, so it can reach
///     neither a private method nor an instance one without an instance; the containing type, which can, hands each
///     method to the spec's constructor as a delegate, and the spec calls the delegate instead.
/// </summary>
internal sealed class HelperDelegates
{
    private const string CallAnnotationKind = "Motiv.HelperCall";
    private const int MaxFuncParameters = 16;

    private const string SystemPrefix = "System.";

    private static readonly HelperDelegates Unpassable = new([], ImmutableDictionary<string, HelperCall>.Empty)
    {
        CanBePassed = false
    };

    private readonly ImmutableArray<Helper> _helpers;
    private readonly ImmutableDictionary<string, HelperCall> _calls;

    private HelperDelegates(ImmutableArray<Helper> helpers, ImmutableDictionary<string, HelperCall> calls)
    {
        _helpers = helpers;
        _calls = calls;
    }

    /// <summary>
    ///     Whether every called method can be passed as a delegate; when one cannot — a local function, a call that
    ///     does not bind, a <c>ref</c>, <c>out</c> or <c>in</c> parameter, a <c>params</c> array it expands, a default
    ///     value it cannot restate, or a type argument of the enclosing method's own, which the spec cannot name — the
    ///     expression gets no fix.
    /// </summary>
    public bool CanBePassed { get; private set; } = true;

    public bool IsEmpty => _helpers.IsEmpty;

    /// <summary>
    ///     Whether the file must import <c>System</c> for the delegates' <c>Func</c>, which is named without its
    ///     namespace so the spec's constructor reads as it would by hand.
    /// </summary>
    public bool NeedsSystemNamespace => _helpers.Any(helper => helper.NeedsSystemNamespace);

    /// <summary>
    ///     The types the delegates carry, whose type parameters of the containing type the spec must declare even
    ///     when the expression names none of them itself.
    /// </summary>
    public IEnumerable<ITypeSymbol> SignatureTypes =>
        _helpers.SelectMany(helper => helper.Method.Parameters.Select(parameter => parameter.Type).Append(helper.Method.ReturnType));

    /// <summary>Whether a delegate is bound to <c>this</c>, so the spec can only be built by an instance.</summary>
    public bool AnyInstance => _helpers.Any(helper => !helper.Method.IsStatic);

    /// <summary>The spec's constructor parameters, one delegate per method: <c>Func&lt;int, bool&gt; isSmall</c>.</summary>
    public IEnumerable<ParameterSyntax> Parameters =>
        _helpers.Select(helper => Parameter(Identifier(helper.ParameterName)).WithType(ParseTypeName(helper.DelegateType)));

    /// <summary>The method groups the containing type passes to the spec's constructor: <c>(IsSmall, IsDefault&lt;int&gt;)</c>.</summary>
    public ArgumentListSyntax ConstructorArguments =>
        ArgumentList(SeparatedList(_helpers.Select(helper => Argument(ParseExpression(helper.MethodGroup)))));

    /// <summary>
    ///     Finds the methods <paramref name="expression" /> calls on the containing type or a base type.
    /// </summary>
    public static HelperDelegates Find(
        ExpressionSyntax expression,
        SemanticModel semanticModel,
        INamedTypeSymbol? containingType,
        int specPosition)
    {
        var helpers = new List<Helper>();
        var calls = ImmutableDictionary.CreateBuilder<string, HelperCall>();

        foreach (var invocation in expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
        {
            // A qualified call (x.M(), Type.M()) reaches its method the way the spec can
            if (invocation.Expression is not SimpleNameSyntax name || IsContextualKeyword(name))
                continue;

            if (semanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
                return Unpassable;

            if (!IsMemberOf(method, containingType))
                continue;

            if (method.MethodKind == MethodKind.LocalFunction
                || !CanBeDelegate(method)
                || semanticModel.GetOperation(invocation) is not IInvocationOperation operation
                || ArgumentSources(invocation, operation, semanticModel, specPosition) is not { } arguments)
            {
                return Unpassable;
            }

            var helper = helpers.FirstOrDefault(existing => SymbolEqualityComparer.Default.Equals(existing.Method, method));
            if (helper is null)
            {
                var delegateType = DelegateType(method, semanticModel, specPosition, out var needsSystemNamespace);
                helper = new Helper(method, delegateType, needsSystemNamespace, MethodGroup(method, semanticModel, specPosition));
                helpers.Add(helper);
            }

            // By the full span: a call on the helper's result, Describe(n).Contains(x), starts where the helper does
            calls[invocation.Span.ToString()] = new HelperCall(helper, arguments);
        }

        // The spec lambda's parameter is a value's own name, or m for the model, so no delegate may take either
        var namesInScope = new HashSet<string>(expression.DescendantTokens()
            .Where(token => token.IsKind(SyntaxKind.IdentifierToken))
            .Select(token => token.ValueText)) { "m" };
        NameParameters(helpers, namesInScope);

        return new HelperDelegates([..helpers], calls.ToImmutable());
    }

    /// <summary>
    ///     Marks each call <see cref="Find" /> found, so <see cref="ReplaceCalls" /> can recognise it in a clause
    ///     rebuilt from <paramref name="expression" />, where it no longer sits at its source position.
    /// </summary>
    public ExpressionSyntax Annotate(ExpressionSyntax expression) =>
        _calls.IsEmpty
            ? expression
            : expression.ReplaceNodes(
                expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
                    .Where(invocation => _calls.ContainsKey(invocation.Span.ToString())),
                (original, rewritten) => rewritten.WithAdditionalAnnotations(
                    new SyntaxAnnotation(CallAnnotationKind, original.Span.ToString())));

    /// <summary>
    ///     Replaces each marked call with a call to its delegate, <c>IsBelow(limit: a, n: b)</c> becoming
    ///     <c>isBelow(b, a)</c>: a delegate takes its arguments in order and has no default values.
    /// </summary>
    public ExpressionSyntax ReplaceCalls(ExpressionSyntax clause) =>
        clause.ReplaceNodes(
            clause.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
                .Where(invocation => invocation.HasAnnotations(CallAnnotationKind)),
            (original, rewritten) =>
            {
                var call = _calls[original.GetAnnotations(CallAnnotationKind).First().Data!];
                var arguments = call.Arguments.Select(source => source.Index is { } index
                    ? rewritten.ArgumentList.Arguments[index].WithNameColon(null)
                    : Argument(source.DefaultValue!));

                return InvocationExpression(
                        IdentifierName(call.Helper.ParameterName),
                        ArgumentList(SeparatedList(arguments)))
                    .WithTriviaFrom(rewritten);
            });

    private static bool IsContextualKeyword(SimpleNameSyntax name) =>
        SyntaxFacts.GetContextualKeywordKind(name.Identifier.ValueText) != SyntaxKind.None;

    private static bool IsMemberOf(IMethodSymbol method, INamedTypeSymbol? containingType)
    {
        for (var type = containingType; type is not null; type = type.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(method.ContainingType, type))
                return true;
        }

        return false;
    }

    // A Func carries neither a ref-kind parameter nor more than 16, and the spec cannot name the enclosing method's
    // type parameters, which it declares nowhere
    private static bool CanBeDelegate(IMethodSymbol method) =>
        !method.ReturnsVoid
        && !method.ReturnsByRef
        && !method.ReturnsByRefReadonly
        && method.Parameters.Length <= MaxFuncParameters
        && method.Parameters.All(parameter => parameter.RefKind == RefKind.None)
        && !method.Parameters.Select(parameter => parameter.Type).Append(method.ReturnType).Any(NamesMethodTypeParameter);

    private static bool NamesMethodTypeParameter(ITypeSymbol type) =>
        type switch
        {
            ITypeParameterSymbol typeParameter => typeParameter.TypeParameterKind == TypeParameterKind.Method,
            INamedTypeSymbol named => named.TypeArguments.Any(NamesMethodTypeParameter),
            IArrayTypeSymbol array => NamesMethodTypeParameter(array.ElementType),
            _ => false
        };

    /// <summary>
    ///     Where each of the delegate's arguments comes from, in parameter order: the call's own argument, by its
    ///     position in the call, or the default value the call left out.
    /// </summary>
    private static ImmutableArray<ArgumentSource>? ArgumentSources(
        InvocationExpressionSyntax invocation,
        IInvocationOperation operation,
        SemanticModel semanticModel,
        int specPosition)
    {
        // Named arguments put back in parameter order run in that order, which only matters when one has effects
        var arguments = operation.Arguments.OrderBy(argument => argument.Parameter!.Ordinal).ToList();
        if (!arguments.SequenceEqual(operation.Arguments)
            && invocation.ArgumentList.Arguments.Any(argument => argument.Expression.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Any()))
        {
            return null;
        }

        var sources = ImmutableArray.CreateBuilder<ArgumentSource>();
        foreach (var argument in arguments)
        {
            switch (argument.ArgumentKind)
            {
                case ArgumentKind.Explicit when argument.Syntax is ArgumentSyntax syntax:
                    sources.Add(new ArgumentSource(invocation.ArgumentList.Arguments.IndexOf(syntax), null));
                    break;

                case ArgumentKind.DefaultValue when DefaultValue(argument, semanticModel, specPosition) is { } value:
                    sources.Add(new ArgumentSource(null, value));
                    break;

                default:
                    return null;
            }
        }

        return sources.ToImmutable();
    }

    /// <summary>
    ///     The value the compiler passes for an omitted argument — the declared default, or for a caller-info
    ///     parameter the caller's name or line — which only a constant can restate.
    /// </summary>
    private static ExpressionSyntax? DefaultValue(IArgumentOperation argument, SemanticModel semanticModel, int specPosition)
    {
        if (!argument.Value.ConstantValue.HasValue)
            return null;

        var parameter = argument.Parameter!;
        var value = argument.Value.ConstantValue.Value;
        if (value is null)
            return LiteralExpression(SyntaxKind.DefaultLiteralExpression, Token(SyntaxKind.DefaultKeyword));

        // NaN and the infinities have no literal
        if (value is double or float && IsNotFinite(System.Convert.ToDouble(value)))
            return null;

        var literal = ParseExpression(SymbolDisplay.FormatPrimitive(value, quoteStrings: true, useHexadecimalNumbers: false)!);

        // A literal names its own type; anything it does not match exactly is cast to the parameter's
        return value is int or string or bool or char or double && parameter.Type.TypeKind != TypeKind.Enum
            ? literal
            : CastExpression(
                ParseTypeName(parameter.Type.ToMinimalDisplayString(semanticModel, specPosition)),
                ParenthesizedExpression(literal));
    }

    // Func is named without its namespace, which the file then imports, so the constructor reads as it would by hand
    private static bool IsNotFinite(double value) => double.IsNaN(value) || double.IsInfinity(value);

    private static string DelegateType(IMethodSymbol method, SemanticModel semanticModel, int specPosition, out bool needsSystemNamespace)
    {
        var typeArguments = method.Parameters.Select(parameter => parameter.Type).Append(method.ReturnType).ToArray();
        var func = semanticModel.Compilation.GetTypeByMetadataName($"System.Func`{typeArguments.Length}")!;
        var name = func.Construct(typeArguments).ToMinimalDisplayString(semanticModel, specPosition);

        needsSystemNamespace = name.StartsWith(SystemPrefix, StringComparison.Ordinal);
        return needsSystemNamespace ? name.Substring(SystemPrefix.Length) : name;
    }

    // The type arguments are written out rather than inferred from the delegate, which overloads can make ambiguous
    private static string MethodGroup(IMethodSymbol method, SemanticModel semanticModel, int specPosition) =>
        method.TypeArguments.IsEmpty
            ? method.Name
            : $"{method.Name}<{string.Join(", ", method.TypeArguments.Select(type => type.ToMinimalDisplayString(semanticModel, specPosition)))}>";

    /// <summary>
    ///     Names each delegate after its method, and an overload after its parameter types too, so two overloads
    ///     become <c>isSmallInt32</c> and <c>isSmallInt64</c>; a name already taken is numbered.
    /// </summary>
    private static void NameParameters(List<Helper> helpers, HashSet<string> taken)
    {
        foreach (var overloads in helpers.GroupBy(helper => helper.Method.Name))
        {
            foreach (var helper in overloads)
            {
                var baseName = overloads.Count() == 1
                    ? helper.Method.Name.ToCamelCase()
                    : helper.Method.Name.ToCamelCase() + string.Concat(helper.Method.Parameters.Select(parameter => parameter.Type.Name));

                var name = baseName;
                for (var suffix = 2; !taken.Add(name); suffix++)
                    name = $"{baseName}{suffix}";

                helper.ParameterName = SyntaxFacts.GetKeywordKind(name) == SyntaxKind.None ? name : $"@{name}";
            }
        }
    }

    private sealed class Helper(IMethodSymbol method, string delegateType, bool needsSystemNamespace, string methodGroup)
    {
        public IMethodSymbol Method { get; } = method;
        public string DelegateType { get; } = delegateType;
        public bool NeedsSystemNamespace { get; } = needsSystemNamespace;
        public string MethodGroup { get; } = methodGroup;
        public string ParameterName { get; set; } = string.Empty;
    }

    private sealed class HelperCall(Helper helper, ImmutableArray<ArgumentSource> arguments)
    {
        public Helper Helper { get; } = helper;
        public ImmutableArray<ArgumentSource> Arguments { get; } = arguments;
    }

    private readonly struct ArgumentSource(int? index, ExpressionSyntax? defaultValue)
    {
        public int? Index { get; } = index;
        public ExpressionSyntax? DefaultValue { get; } = defaultValue;
    }
}
