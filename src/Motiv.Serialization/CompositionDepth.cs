namespace Motiv.Serialization;

/// <summary>
/// How deep the spec tree a document binds to composes, and how much of that depth is decorator
/// nesting.
/// </summary>
/// <param name="Composed">
/// Every level the binder composes — operator folds and decorator wrappers alike. Bounded by
/// <see cref="RuleSerializerOptions.MaxCompositionDepth" />, which is a cost budget.
/// </param>
/// <param name="Decorator">
/// The decorator subset of <paramref name="Composed" />. Bounded by
/// <see cref="RuleSerializerOptions.MaxDecoratorDepth" />, which is a stack budget — see that
/// property's remarks for why the two cannot be one number.
/// </param>
internal readonly record struct CompositionMeasure(int Composed, int Decorator)
{
    /// <summary>The measure of a leaf: a spec that composes nothing and wraps nothing.</summary>
    public static CompositionMeasure Leaf => default;

    /// <summary>This measure with one composition level added, and no decorator level.</summary>
    public CompositionMeasure Composing() => this with { Composed = Composed + 1 };

    /// <summary>This measure with one level added that is a decorator.</summary>
    public CompositionMeasure Decorating() => new(Composed + 1, Decorator + 1);

    /// <summary>The deeper of two measures on each axis independently.</summary>
    public static CompositionMeasure Max(CompositionMeasure left, CompositionMeasure right) =>
        new(Math.Max(left.Composed, right.Composed), Math.Max(left.Decorator, right.Decorator));
}

/// <summary>
/// Measures a rule tree <em>against the source its <c>spec</c> leaves resolve through</em>, which is
/// what <see cref="RuleDocumentParser" />'s own depth check cannot do: the parser has no
/// <see cref="ISpecSource" />, so it scores every reference as a leaf however deep the proposition it
/// names composes.
/// </summary>
/// <remarks>
/// <para>
/// That gap was <see href="https://github.com/karlssberg/Motiv/issues/201">#201</see>. A catalogue
/// composes an alternating operator/decorator shape one authored proposition at a time — every node
/// carrying a <c>name</c> or a <c>whenTrue</c> is wrapped by <c>RuleBinder.Decorate</c>, a named
/// document's root is wrapped by <c>RuleBinder.Bind</c>, and an authored proposition may reference
/// another — and that shape aborts the process at 1,047 levels synchronously and 261 asynchronously
/// on a 1 MB thread. Depth was therefore bounded by how many publishes the authoring surface
/// accepted, not by any cap.
/// </para>
/// <para>
/// The measure a document earns is carried on its <see cref="SpecRegistryEntry" />, so the next
/// document to reference it starts from that depth rather than from zero. A <em>compiled</em> entry
/// carries <see cref="CompositionMeasure.Leaf" />: Motiv has no stack-safe walk of a finished spec
/// tree, and the argument Spec 3E made for leaving decorator nesting unbounded — that its depth is
/// what a developer wrote, not what a request asked for — is true of compiled code even though it was
/// false of a catalogue. So the caps bound what documents compose, and treat what was compiled in as
/// the developer's own budget.
/// </para>
/// <para>
/// Adapters the binder inserts at a leaf — <c>ToExplanationSpec</c> for a metadata entry, the async
/// lift — are not counted. They are a constant per leaf rather than a level that accumulates along a
/// chain, so they cannot carry depth the way a decorator does.
/// </para>
/// </remarks>
internal static class CompositionDepth
{
    /// <summary>Refuses a document whose composed spec would exceed either cap.</summary>
    /// <returns><c>true</c> when an error was reported.</returns>
    public static bool ReportIfTooDeep(
        RuleDocument document,
        ISpecSource source,
        RuleSerializerOptions options,
        List<RuleError> errors)
    {
        var measure = Of(document, source);

        if (measure.Composed > options.MaxCompositionDepth)
            return Report(
                $"document composes deeper than the maximum composition depth of {options.MaxCompositionDepth}",
                errors);

        if (measure.Decorator > options.MaxDecoratorDepth)
            return Report(
                $"document nests more than the maximum decorator depth of {options.MaxDecoratorDepth}",
                errors);

        return false;
    }

    /// <summary>The measure of the spec a whole document binds to, root decoration included.</summary>
    /// <remarks>
    /// Takes a parsed document with a root, as the binders do one line later — every caller reaches
    /// here only once parsing has succeeded without errors, and a rootless document is a parse error.
    /// </remarks>
    public static CompositionMeasure Of(RuleDocument document, ISpecSource source)
    {
        var measure = Of(document.Root!, source);

        // RuleBinder.Bind wraps a named document's root, which is one more decorator level than the
        // root node's own name would account for.
        return document.Name is null ? measure : measure.Decorating();
    }

    /// <summary>The measure of the spec a node binds to.</summary>
    /// <remarks>
    /// Recursion here is bounded by <see cref="RuleSerializerOptions.MaxDocumentDepth" />, which the
    /// parser has already enforced by the time a binder calls this — a reference chain adds depth to
    /// the <em>result</em>, not to this walk, because a referenced proposition's measure is read off
    /// its entry rather than re-walked.
    /// </remarks>
    private static CompositionMeasure Of(RuleNode node, ISpecSource source)
    {
        var measure = OfOperator(node, source);

        // Name and whenTrue are RuleBinder.Decorate's two triggers; an object payload is the
        // metadata binders' equivalent, which re-metadatizes through the same kind of wrapper.
        var decorated = node.Name is not null || node.WhenTrueText is not null || node.HasObjectPayloads;

        return decorated ? measure.Decorating() : measure;
    }

    private static CompositionMeasure OfOperator(RuleNode node, ISpecSource source)
    {
        if (node.Operator == RuleOperator.Spec)
            return source.Find(node.SpecName!)?.Depth ?? CompositionMeasure.Leaf;

        if (node.Children.Count == 0)
            return CompositionMeasure.Leaf;

        var measure = Of(node.Children[0], source);

        // 'not' and the higher-order quantifiers wrap their single operand in one composition level,
        // which the fold below has no second operand to accumulate.
        if (node.Children.Count == 1)
            return measure.Composing();

        // The left-deep fold RuleBinder.BindComposition performs: every operand after the first adds a
        // level over the deepest so far, so a nested operand's depth compounds rather than adds.
        for (var index = 1; index < node.Children.Count; index++)
            measure = CompositionMeasure.Max(measure, Of(node.Children[index], source)).Composing();

        return measure;
    }

    private static bool Report(string message, List<RuleError> errors)
    {
        errors.Add(new RuleError("$.rule", RuleErrorCode.DocumentTooLarge, message));
        return true;
    }
}
