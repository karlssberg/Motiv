namespace Motiv.Serialization.Expressions;

/// <summary>The one entry the binders call for an expression node.</summary>
internal static class LeafBinding
{
#if NET8_0_OR_GREATER
    /// <summary>Parses and checks an expression leaf, reporting problems as ranged errors at <see cref="RuleNode.Path" />.</summary>
    /// <typeparam name="TModel">The model type the leaf is checked against.</typeparam>
    /// <param name="node">The expression node to analyse.</param>
    /// <param name="errors">Accumulates the leaf's problems as ranged errors.</param>
    /// <returns>The completed analysis, or <c>null</c> when parsing failed or the leaf is invalid.</returns>
    public static LeafAnalysis? Analyse<TModel>(RuleNode node, List<RuleError> errors) =>
        Analyse<TModel>(node, errors, out _);

    private static LeafAnalysis? Analyse<TModel>(RuleNode node, List<RuleError> errors, out LeafNode? root)
    {
        var text = node.ExpressionText!;
        var problems = new List<LeafProblem>();
        root = LeafParser.Parse(text, problems);
        if (root is null)
        {
            Report(node, problems, errors);
            return null;
        }

        var scope = LeafScope.For(typeof(TModel), node.ParameterDeclarations ?? []);
        var analysis = LeafChecker.Check(root, scope);
        Report(node, analysis.Problems, errors);
        return analysis.IsValid ? analysis : null;
    }

    /// <summary>Parses, checks and compiles an expression leaf into a spec.</summary>
    /// <typeparam name="TModel">The model type the leaf is compiled against.</typeparam>
    /// <param name="node">The expression node to bind.</param>
    /// <param name="errors">Accumulates the leaf's problems as ranged errors.</param>
    /// <returns>The compiled spec, or <c>null</c> when the leaf failed to parse or check.</returns>
    public static SpecBase<TModel, string>? Bind<TModel>(RuleNode node, List<RuleError> errors)
    {
        // The same root instance that was checked must be the one compiled: LeafAnalysis.Types
        // is keyed by reference, so a second, independent parse of the same text would produce a
        // structurally identical but reference-distinct tree the analysis knows nothing about.
        var analysis = Analyse<TModel>(node, errors, out var root);
        if (analysis is null || root is null) return null;
        return LeafCompiler.Compile<TModel>(root, analysis, node.ParameterValues ?? new Dictionary<string, object?>(), node.ExpressionText!);
    }

    private static void Report(RuleNode node, IEnumerable<LeafProblem> problems, List<RuleError> errors)
    {
        foreach (var problem in problems.Where(p => !p.IsWarning))
            errors.Add(new RuleError(node.Path, problem.Code, problem.Message, new RuleTextRange(problem.Start, problem.End)));
    }
#else
    /// <summary>Reports that expression nodes are not available on this target framework.</summary>
    /// <typeparam name="TModel">The model type the leaf would have been bound against.</typeparam>
    /// <param name="node">The expression node that cannot be bound.</param>
    /// <param name="errors">Accumulates the resulting error.</param>
    /// <returns>Always <c>null</c>.</returns>
    public static SpecBase<TModel, string>? Bind<TModel>(RuleNode node, List<RuleError> errors)
    {
        errors.Add(new RuleError(node.Path, RuleErrorCode.ExpressionsNotEnabled,
            "expression nodes are supported on .NET 8 or later"));
        return null;
    }
#endif
}
