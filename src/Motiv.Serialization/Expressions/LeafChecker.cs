#if NET8_0_OR_GREATER
namespace Motiv.Serialization.Expressions;

internal sealed class LeafAnalysis(IReadOnlyList<LeafProblem> problems, IReadOnlyDictionary<LeafNode, Type> types, IReadOnlyList<LeafFact> facts, IReadOnlyDictionary<LeafNode, LeafScope> lambdaScopes)
{
    public IReadOnlyList<LeafProblem> Problems { get; } = problems;
    public IReadOnlyDictionary<LeafNode, Type> Types { get; } = types;
    public IReadOnlyList<LeafFact> Facts { get; } = facts;
    public IReadOnlyDictionary<LeafNode, LeafScope> LambdaScopes { get; } = lambdaScopes;
    public bool IsValid => Problems.All(p => p.IsWarning);
}

/// <summary>
/// Types a leaf against a scope. Fields and fixed-type methods anchor; literals and parameters
/// are type variables that take the join of their anchors over the lattice; the compiler then
/// reads <see cref="LeafAnalysis.Types" /> for every node.
/// </summary>
internal sealed class LeafChecker
{
    private static readonly HashSet<string> CollectionMethods = ["where", "any", "all", "count", "sum", "min", "max"];
    private readonly LeafScope _root;
    private readonly List<LeafProblem> _problems = [];
    private readonly Dictionary<LeafNode, LeafType> _types = new(ReferenceEqualityComparer.Instance);
    private readonly List<TypeVar> _vars = [];
    private readonly HashSet<TypeVar> _errored = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<LeafNode, LeafScope> _lambdaScopes = new(ReferenceEqualityComparer.Instance);

    private LeafChecker(LeafScope root) { _root = root; }

    public static LeafAnalysis Check(LeafNode root, LeafScope scope)
    {
        var checker = new LeafChecker(scope);
        var type = checker.Visit(root, scope);
        checker.DefaultUnresolved();
        if (checker.Resolve(type) is { } concrete && (Nullable.GetUnderlyingType(concrete) ?? concrete) != typeof(bool))
            checker.Report(root, RuleErrorCode.ExpressionTypeMismatch, $"a leaf must be a condition; this is {Describe(concrete)}");
        return checker.Finish(root);
    }

    // ---- results ---------------------------------------------------------------------------

    private LeafAnalysis Finish(LeafNode root)
    {
        var types = new Dictionary<LeafNode, Type>(ReferenceEqualityComparer.Instance);
        foreach (var (node, type) in _types)
            if (Resolve(type) is { } concrete) types[node] = concrete;

        var facts = new List<LeafFact>();
        foreach (var (node, type) in _types)
        {
            if (node is not (NumberLiteral or ParameterRef) || type.Var is null) continue;
            if (!types.TryGetValue(node, out var nodeType)) continue;
            var var = type.Var.Root;
            facts.Add(new LeafFact(node, nodeType, var.ResolvedBy));
        }
        if (types.TryGetValue(root, out var rootType))
            facts.Add(new LeafFact(root, rootType, null));
        return new LeafAnalysis(_problems, types, facts, _lambdaScopes);
    }

    private Type? Resolve(LeafType type)
    {
        if (type.Concrete is not null) return type.Concrete;
        if (type.Var?.Root.Resolved is { } kind) return NumericLattice.ClrType(kind);
        return null;
    }

    private void DefaultUnresolved()
    {
        foreach (var var in _vars.Select(v => v.Root).Distinct())
        {
            if (var.Resolved is not null) continue;
            if (_errored.Contains(var)) continue;
            var.Resolved = var.Fractional || var.ParamKind == RuleParameterType.Number ? NumericKind.Decimal : NumericKind.Int32;
            var first = var.Members[0];
            _problems.Add(new LeafProblem(RuleErrorCode.ExpressionTypeMismatch,
                $"no model field fixes the type of this expression; assuming {Describe(NumericLattice.ClrType(var.Resolved.Value))}",
                first.Start, first.End, IsWarning: true));
        }
    }

    private void Report(LeafNode at, RuleErrorCode code, string message, bool warning = false) =>
        _problems.Add(new LeafProblem(code, message, at.Start, at.End, warning));

    private void Report(int start, int end, RuleErrorCode code, string message) =>
        _problems.Add(new LeafProblem(code, message, start, end));

    private static string Describe(Type type)
    {
        var underlying = Nullable.GetUnderlyingType(type) ?? type;
        if (underlying == typeof(int)) return "int";
        if (underlying == typeof(long)) return "long";
        if (underlying == typeof(decimal)) return "decimal";
        if (underlying == typeof(double)) return "double";
        if (underlying == typeof(float)) return "float";
        if (underlying == typeof(bool)) return "a condition";
        if (underlying == typeof(string)) return "string";
        if (LeafScope.ElementType(underlying) is { } element) return $"a collection of {Describe(element)}";
        return underlying.Name;
    }

    private LeafType Set(LeafNode node, LeafType type) { _types[node] = type; return type; }

    private LeafType NewVar(LeafNode node, bool fractional, RuleParameterType? paramKind)
    {
        var var = new TypeVar { Fractional = fractional, ParamKind = paramKind };
        var.Members.Add(node);
        _vars.Add(var);
        return Set(node, LeafType.OfVar(var));
    }

    // ---- the walk --------------------------------------------------------------------------

    private LeafType Visit(LeafNode node, LeafScope scope) => node switch
    {
        NumberLiteral n => NewVar(n, n.Text.Contains('.'), null),
        StringLiteral s => Set(s, LeafType.Of(typeof(string))),
        BoolLiteral b => Set(b, LeafType.Of(typeof(bool))),
        NullLiteral n => Set(n, LeafType.Of(typeof(object))),
        ParameterRef p => VisitParameter(p, scope),
        Identifier i => VisitIdentifier(i, scope),
        MemberAccess m => VisitMember(m, scope),
        MethodCall c => VisitCall(c, scope),
        Lambda l => Fail(l, RuleErrorCode.InvalidExpression, "a lambda is only allowed as a method argument"),
        Unary u => VisitUnary(u, scope),
        Binary b => VisitBinary(b, scope),
        _ => LeafType.Unknown,
    };

    private LeafType Fail(LeafNode node, RuleErrorCode code, string message)
    {
        Report(node, code, message);
        return Set(node, LeafType.Unknown);
    }

    private LeafType VisitParameter(ParameterRef p, LeafScope scope)
    {
        if (!scope.Parameters.TryGetValue(p.Name, out var declaration))
            return Fail(p, RuleErrorCode.UnknownField, $"unknown parameter '@{p.Name}'");
        return declaration.Type switch
        {
            RuleParameterType.Integer or RuleParameterType.Number => NewVar(p, false, declaration.Type),
            RuleParameterType.String => Set(p, LeafType.Of(typeof(string))),
            _ => Set(p, LeafType.Of(typeof(bool))),
        };
    }

    private LeafType VisitIdentifier(Identifier i, LeafScope scope)
    {
        if (scope.Variables.TryGetValue(i.Name, out var variable))
            return Set(i, LeafType.Of(variable));
        var member = LeafScope.FindMember(scope.ModelType, i.Name);
        if (member is null)
            return Fail(i, RuleErrorCode.UnknownField, $"'{i.Name}' is not a field of {scope.ModelType.Name}");
        return Set(i, LeafType.Of(LeafScope.MemberType(member)));
    }

    private LeafType VisitMember(MemberAccess m, LeafScope scope)
    {
        var target = Visit(m.Target, scope);
        if (target.IsUnknown) return Set(m, LeafType.Unknown);
        var targetType = target.Underlying ?? NumericLattice.ClrType(target.Var!.Root.Resolved ?? NumericKind.Int32);
        if (LeafScope.ElementType(targetType) is not null)
        {
            Report(m.NameStart, m.NameEnd, RuleErrorCode.UnknownMethod, $"'{Print(m.Target)}' is a collection — use .where(…), .any(…), .all(…), .count(), .sum(…), .min(…) or .max(…) on it");
            return Set(m, LeafType.Unknown);
        }
        if (targetType.IsPrimitive || targetType == typeof(string) || targetType == typeof(decimal))
        {
            Report(m.NameStart, m.NameEnd, RuleErrorCode.UnknownField, $"'{m.Name}' is not a field of {Describe(targetType)}");
            return Set(m, LeafType.Unknown);
        }
        var member = LeafScope.FindMember(targetType, m.Name);
        if (member is null)
        {
            Report(m.NameStart, m.NameEnd, RuleErrorCode.UnknownField, $"'{m.Name}' is not a field of {targetType.Name}");
            return Set(m, LeafType.Unknown);
        }
        return Set(m, LeafType.Of(Lift(LeafScope.MemberType(member), target.IsNullable)));
    }

    /// <summary>A value reached through a nullable path is itself nullable.</summary>
    private static Type Lift(Type type, bool throughNullable)
    {
        if (!throughNullable || !type.IsValueType || Nullable.GetUnderlyingType(type) is not null) return type;
        return typeof(Nullable<>).MakeGenericType(type);
    }

    private LeafType VisitCall(MethodCall c, LeafScope scope)
    {
        var target = Visit(c.Target, scope);
        if (target.IsUnknown) return Set(c, LeafType.Unknown);
        var targetType = target.Underlying;

        if (c.Method == "equalsIgnoreCase")
        {
            if (targetType != typeof(string))
                return FailAt(c, RuleErrorCode.UnknownMethod, $"'equalsIgnoreCase' needs a string; this is {Describe(targetType ?? typeof(object))}");
            if (c.Arguments.Count != 1 || c.Arguments[0] is Lambda)
                return FailAt(c, RuleErrorCode.UnknownMethod, "'equalsIgnoreCase' takes one string argument");
            var argument = Visit(c.Arguments[0], scope);
            if (argument.Underlying is { } at && at != typeof(string))
                Report(c.Arguments[0], RuleErrorCode.ExpressionTypeMismatch, $"'equalsIgnoreCase' expects a string; this is {Describe(at)}");
            return Set(c, LeafType.Of(typeof(bool)));
        }

        var element = targetType is null ? null : LeafScope.ElementType(targetType);
        if (element is null)
            return FailAt(c, RuleErrorCode.UnknownMethod, $"'.{c.Method}()' needs a collection; this is {Describe(targetType ?? typeof(object))}");
        if (!CollectionMethods.Contains(c.Method))
            return FailAt(c, RuleErrorCode.UnknownMethod, $"unknown method '{c.Method}'; the collection methods are where, any, all, count, sum, min, max");

        var nullable = target.IsNullable;
        if (c.Method == "count")
        {
            if (c.Arguments.Count > 0)
                Report(c.Arguments[0], RuleErrorCode.UnknownMethod, "'count()' takes no arguments — filter with .where(…) first");
            return Set(c, LeafType.Of(Lift(typeof(int), nullable)));
        }

        if (c.Arguments.Count != 1 || c.Arguments[0] is not Lambda lambda)
            return FailAt(c, RuleErrorCode.UnknownMethod, $"'{c.Method}' takes a lambda: {c.Method}(x => …)");

        var inner = scope.WithVariable(lambda.Parameter, element);
        _lambdaScopes[lambda] = inner;
        Set(lambda, LeafType.Of(element));
        var body = Visit(lambda.Body, inner);
        var bodyType = Resolve(body);

        var wantsCondition = c.Method is "where" or "any" or "all";
        if (wantsCondition)
        {
            if (bodyType is not null && (Nullable.GetUnderlyingType(bodyType) ?? bodyType) != typeof(bool))
            {
                Report(lambda.Body, RuleErrorCode.ExpressionTypeMismatch, $"'{c.Method}' expects a condition; this is {Describe(bodyType)}");
                return Set(c, LeafType.Unknown);
            }
            return Set(c, LeafType.Of(c.Method == "where" ? targetType! : Lift(typeof(bool), nullable)));
        }

        // sum / min / max: numeric body; a literal-only body stays a variable so `sum(o => 1)` still types.
        if (bodyType is null)
            return Set(c, body.Var is not null ? body : LeafType.Unknown);
        if (NumericLattice.KindOf(bodyType) is null)
        {
            Report(lambda.Body, RuleErrorCode.ExpressionTypeMismatch, $"'{c.Method}' expects a number; this is {Describe(bodyType)}");
            return Set(c, LeafType.Unknown);
        }
        if (body.Var is not null)
            return Set(c, body);
        return Set(c, LeafType.Of(Lift(bodyType, nullable)));
    }

    private LeafType FailAt(MethodCall c, RuleErrorCode code, string message)
    {
        Report(c.MethodStart, c.MethodEnd, code, message);
        return Set(c, LeafType.Unknown);
    }

    private LeafType VisitUnary(Unary u, LeafScope scope)
    {
        var operand = Visit(u.Operand, scope);
        if (u.Operator == "!")
        {
            if (operand.Underlying is { } t && t != typeof(bool))
                Report(u, RuleErrorCode.ExpressionTypeMismatch, $"'!' needs a condition; this is {Describe(t)}");
            return Set(u, LeafType.Of(typeof(bool)));
        }
        if (operand.Var is null && operand.Underlying is { } n && NumericLattice.KindOf(n) is null)
            Report(u, RuleErrorCode.ExpressionTypeMismatch, $"'-' needs a number; this is {Describe(n)}");
        return Set(u, operand);
    }

    private LeafType VisitBinary(Binary b, LeafScope scope)
    {
        var left = Visit(b.Left, scope);
        var right = Visit(b.Right, scope);

        switch (b.Operator)
        {
            case "&&" or "||":
                foreach (var (side, type) in new[] { (b.Left, left), (b.Right, right) })
                    if (type.Underlying is { } t && t != typeof(bool))
                        Report(side, RuleErrorCode.ExpressionTypeMismatch, $"'{b.Operator}' needs conditions on both sides; this is {Describe(t)}");
                    else if (type.Var is not null)
                        Report(side, RuleErrorCode.ExpressionTypeMismatch, $"'{b.Operator}' needs conditions on both sides; this is a number");
                return Set(b, LeafType.Of(typeof(bool)));

            case "==" or "!=":
                return Set(b, VisitEquality(b, left, right));

            case "<" or "<=" or ">" or ">=":
                Unify(b, left, right, arithmetic: false);
                return Set(b, LeafType.Of(typeof(bool)));

            default:
            {
                var result = Unify(b, left, right, arithmetic: true);
                if (b.Operator == "/" && result.Concrete is { } rt && NumericLattice.KindOf(rt) is { } rk && NumericLattice.IsIntegral(rk))
                    Report(b, RuleErrorCode.ExpressionTypeMismatch, "integer division truncates; compare against a fractional value to keep the remainder", warning: true);
                else if (b.Operator == "/" && result.Var?.Root is { Resolved: null, Fractional: false })
                    Report(b, RuleErrorCode.ExpressionTypeMismatch, "integer division truncates; compare against a fractional value to keep the remainder", warning: true);
                return Set(b, result);
            }
        }
    }

    private LeafType VisitEquality(Binary b, LeafType left, LeafType right)
    {
        if (b.Left is NullLiteral || b.Right is NullLiteral)
        {
            var other = b.Left is NullLiteral ? right : left;
            var otherNode = b.Left is NullLiteral ? b.Right : b.Left;
            if (other.Var is not null || (other.Concrete is not null && !other.IsNullable))
                Report(otherNode, RuleErrorCode.ExpressionTypeMismatch, $"'{Print(otherNode)}' is never null", warning: true);
            return LeafType.Of(typeof(bool));
        }

        var l = left.Underlying; var r = right.Underlying;
        if (l is not null && r is not null && NumericLattice.KindOf(l) is null && NumericLattice.KindOf(r) is null)
        {
            if (l != r)
                Report(b, RuleErrorCode.ExpressionTypeMismatch, $"comparing {Describe(l)} with {Describe(r)}");
            return LeafType.Of(typeof(bool));
        }
        if ((l is not null && NumericLattice.KindOf(l) is null) || (r is not null && NumericLattice.KindOf(r) is null))
        {
            var (nonNumeric, node) = l is not null && NumericLattice.KindOf(l) is null ? (l, b.Left) : (r!, b.Right);
            Report(b, RuleErrorCode.ExpressionTypeMismatch, $"comparing {Describe(nonNumeric)} with a number");
            return LeafType.Of(typeof(bool));
        }
        Unify(b, left, right, arithmetic: false);
        return LeafType.Of(typeof(bool));
    }

    /// <summary>
    /// Gives two numeric operands one type. Returns the operands' common type (for arithmetic,
    /// the result type). Non-numeric concrete operands are reported here for arithmetic and
    /// ordering; equality handles its own.
    /// </summary>
    private LeafType Unify(Binary b, LeafType left, LeafType right, bool arithmetic)
    {
        if (left.IsUnknown || right.IsUnknown)
        {
            // The other side's var has no genuine anchor to blame — an unrelated error already
            // fired on this expression, so don't also pile on a "no model field fixes this" default.
            if (left.Var is not null) _errored.Add(left.Var.Root);
            if (right.Var is not null) _errored.Add(right.Var.Root);
            return LeafType.Unknown;
        }
        var what = arithmetic ? $"'{b.Operator}' needs numbers" : $"'{b.Operator}' compares numbers";

        foreach (var (side, type) in new[] { (b.Left, left), (b.Right, right) })
            if (type.Underlying is { } t && NumericLattice.KindOf(t) is null)
            {
                Report(side, RuleErrorCode.ExpressionTypeMismatch, $"{what}; this is {Describe(t)}");
                return LeafType.Unknown;
            }

        var nullable = left.IsNullable || right.IsNullable;

        if (left.Concrete is { } lc && right.Concrete is { } rc)
        {
            var lk = NumericLattice.KindOf(lc)!.Value; var rk = NumericLattice.KindOf(rc)!.Value;
            var join = NumericLattice.Join(lk, rk);
            if (join is null)
            {
                Report(b, RuleErrorCode.ExpressionTypeMismatch, $"cannot compare {Describe(lc)} with {Describe(rc)} without losing precision; use a registered spec for this comparison");
                return LeafType.Unknown;
            }
            return LeafType.Of(Lift(NumericLattice.ClrType(join.Value), nullable));
        }

        if (left.Var is not null && right.Var is not null)
        {
            var a = left.Var.Root; var c = right.Var.Root;
            if (a != c)
            {
                if (a.Resolved is { } ar && c.Resolved is { } cr)
                {
                    var join = NumericLattice.Join(ar, cr);
                    if (join is null) { Report(b, RuleErrorCode.ExpressionTypeMismatch, $"cannot combine {Describe(NumericLattice.ClrType(ar))} with {Describe(NumericLattice.ClrType(cr))}"); _errored.Add(a); _errored.Add(c); return LeafType.Unknown; }
                    a.Resolved = join;
                }
                else
                {
                    a.Resolved ??= c.Resolved;
                    a.ResolvedBy ??= c.ResolvedBy;
                }
                a.Fractional |= c.Fractional;
                // Two parameters of different declared kinds (integer vs number) have no common
                // integral type that honors both constraints — the merged var must allow only
                // decimal, so force it fractional rather than silently keeping just one side's kind.
                if (a.ParamKind is not null && c.ParamKind is not null && a.ParamKind != c.ParamKind)
                    a.Fractional = true;
                a.ParamKind ??= c.ParamKind;
                a.Members.AddRange(c.Members);
                c.Parent = a;
            }
            return LeafType.OfVar(a);
        }

        var (var, concrete, anchor) = left.Var is not null ? (left.Var.Root, right.Concrete!, b.Right) : (right.Var!.Root, left.Concrete!, b.Left);
        var kind = NumericLattice.KindOf(concrete)!.Value;
        var target = kind;
        if (var.Fractional && NumericLattice.IsIntegral(kind))
            target = NumericKind.Decimal;
        if (!var.Allows(target) || (var.Resolved is { } already && NumericLattice.Join(already, target) is null))
        {
            var have = var.Resolved is { } h ? Describe(NumericLattice.ClrType(h)) : var.ParamKind == RuleParameterType.Number ? "a number parameter" : "this literal";
            Report(b, RuleErrorCode.ExpressionTypeMismatch, $"cannot use {have} with {Describe(concrete)} without losing precision");
            _errored.Add(var);
            return LeafType.Unknown;
        }
        var.Resolved = var.Resolved is { } prior ? NumericLattice.Join(prior, target) : target;
        var.ResolvedBy ??= Print(anchor);
        return LeafType.Of(Lift(NumericLattice.ClrType(var.Resolved!.Value), nullable));
    }

    /// <summary>The canonical text of a node — the anchor named in a fact.</summary>
    public static string Print(LeafNode node) => node switch
    {
        NumberLiteral n => n.Text,
        StringLiteral s => $"\"{s.Value}\"",
        BoolLiteral b => b.Value ? "true" : "false",
        NullLiteral => "null",
        ParameterRef p => $"@{p.Name}",
        Identifier i => i.Name,
        MemberAccess m => $"{Print(m.Target)}.{m.Name}",
        MethodCall c => $"{Print(c.Target)}.{c.Method}({string.Join(", ", c.Arguments.Select(Print))})",
        Lambda l => $"{l.Parameter} => {Print(l.Body)}",
        Unary u => $"{u.Operator}{Print(u.Operand)}",
        Binary b => $"{Print(b.Left)} {b.Operator} {Print(b.Right)}",
        _ => string.Empty,
    };
}
#endif
