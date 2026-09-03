using System.Diagnostics;
using Motiv.And;
using Motiv.AndAlso;
using Motiv.Not;
using Motiv.Or;
using Motiv.OrElse;
using Motiv.Shared;
using Motiv.Traversal;
using Motiv.XOr;

namespace Motiv;

/// <summary>Represents a base class for boolean results.</summary>
[DebuggerDisplay("{Reason}")]
public abstract class BooleanResultBase
    : IEquatable<BooleanResultBase>, IEquatable<bool>
{
    /// <summary>Prevent inheritance from outside of this project/assembly.</summary>
    internal BooleanResultBase()
    {
    }

    /// <summary>Gets a value indicating whether the condition is satisfied.</summary>
    public abstract bool Satisfied { get; }

    /// <summary>
    /// Gets a concise reason for the result. If the result is a proposition, then the reason will be a single
    /// assertion. Otherwise, the result is boolean expression, and the reason will be a composition of assertions
    /// using the logical operators  &amp;, | and ^.
    /// </summary>
    /// <Remarks>
    /// <para>
    /// The source of the text used for the <see cref="Reason" /> property will either come from the <c>WhenTrue()</c>
    /// or <c>WhenFalse()</c> methods, or from the <c>Create()</c> method.
    /// </para>
    /// <para>
    /// If the <c>WhenTrue()</c> or <c>WhenFalse()</c> methods received either a <c>string</c>, or a function that returns a
    /// <c>string</c>, then the <see cref="Reason"/> property will use these values.
    /// Otherwise, it will use the propositional statement provided to the <c>Create()</c> method,
    /// with negations prefixed with a <c>!</c>.
    /// For example, <c>"!is even"</c>.
    /// </para>
    /// </Remarks>
    public string Reason => Description.Reason;

    /// <summary>
    /// Gets a set of human-readable expressions that represent the underlying resons/boolean-expressons of the result.
    /// </summary>
    public IEnumerable<string> UnderlyingReasons => field ??= UnderlyingExpressionResults.Select(result => result.Reason).ToArray();

    /// <summary>Gets the underlying <see cref="BooleanResultBase" />s that represent the expression results.</summary>
    public IEnumerable<BooleanResultBase> UnderlyingExpressionResults =>
        _underlyingExpressionResults ??= PostOrderFold.Fold(
            this,
            AllCauses,
            CombineExpressionResults,
            ReadUnderlyingExpressionResults,
            WriteUnderlyingExpressionResults);

    private BooleanResultBase[]? _underlyingExpressionResults;

    private static readonly Func<BooleanResultBase, IReadOnlyList<BooleanResultBase>> AllCauses =
        result => result.Causes.AsList();

    private static readonly Func<BooleanResultBase, IReadOnlyList<BooleanResultBase[]>, BooleanResultBase[]>
        CombineExpressionResults = (result, foldedCauses) =>
        {
            var expressionResults = new List<BooleanResultBase>();
            var nextCause = 0;

            foreach (var cause in result.Causes)
            {
                // An operation reached from a non-operation is where one expression ends and the next
                // begins, so that boundary result is itself an expression result.
                if (result is not IBooleanOperationResult && cause is IBooleanOperationResult)
                    expressionResults.Add(cause);

                expressionResults.AddRange(foldedCauses[nextCause++]);
            }

            return expressionResults.ToArray();
        };

    private static readonly Func<BooleanResultBase, BooleanResultBase[]?> ReadUnderlyingExpressionResults =
        result => result._underlyingExpressionResults;

    private static readonly Action<BooleanResultBase, BooleanResultBase[]> WriteUnderlyingExpressionResults =
        (result, results) => result._underlyingExpressionResults = results;

    /// <summary>Gets a full hierarchical breakdown of the reasons for the result.</summary>
    public string Justification => Description.Justification;

    /// <summary>Gets the assertions that determined this result.</summary>
    public IEnumerable<string> Assertions => field ??= Explanation.Assertions.DistinctWithOrderPreserved().ToArray();

    /// <summary>Gets all the assertions yielded by the current result, including those that are non-determinative.</summary>
    /// <remarks>This will yield assertions from both satisfied and unsatisfied operands. </remarks>
    public IEnumerable<string> AllAssertions =>
        _allAssertions ??= PostOrderFold.Fold(
            this,
            BinaryOperands,
            CombineAllAssertions,
            ReadAllAssertions,
            WriteAllAssertions);

    private string[]? _allAssertions;

    private static readonly Func<BooleanResultBase, IReadOnlyList<BooleanResultBase>> BinaryOperands =
        result => result is IBinaryBooleanOperationResult
            ? result.Underlying.AsList()
            : [];

    private static readonly Func<BooleanResultBase, IReadOnlyList<string[]>, string[]> CombineAllAssertions =
        (result, foldedOperands) =>
        {
            if (result is not IBinaryBooleanOperationResult)
                return result.Assertions as string[] ?? result.Assertions.ToArray();

            var assertions = new List<string>();
            for (var i = 0; i < foldedOperands.Count; i++)
                assertions.AddRange(foldedOperands[i]);

            return assertions.ToArray();
        };

    private static readonly Func<BooleanResultBase, string[]?> ReadAllAssertions = result => result._allAssertions;

    private static readonly Action<BooleanResultBase, string[]> WriteAllAssertions =
        (result, assertions) => result._allAssertions = assertions;

    /// <summary>Gets all the determinative assertions from the underlying results.</summary>
    public IEnumerable<string> SubAssertions => field ??= Explanation.Underlying.GetAssertions().ToArray();

    /// <summary>Gets all the assertions from the underlying results.</summary>
    public IEnumerable<string> AllSubAssertions => field ??= Explanation.AllUnderlying.GetAllAssertions().ToArray();

    /// <summary>Gets all the assertions returned from atomic propositions.</summary>
    public IEnumerable<string> RootAssertions => field ??= this.GetRootAssertions().ToArray();

    /// <summary>Gets all the underlying assertions generated by the evaluation.</summary>
    public IEnumerable<string> AllRootAssertions => field ??= this.GetAllRootAssertions().ToArray();

    /// <summary>Gets a set of human-readable descriptions of the underlying causes.</summary>
    public abstract ResultDescriptionBase Description { get; }

    /// <summary>
    /// Gets the specific underlying reasons why the condition is satisfied or not. Duplicates are permitted in the
    /// result at this stage to avoid excessive de-duplication during intermediate steps.  Deduplication is performed during
    /// the call to <see cref="Explanation" />.
    /// </summary>
    public abstract Explanation Explanation { get; }

    /// <summary>Gets the underlying <see cref="BooleanResultBase" />s that caused this result.</summary>
    public abstract IEnumerable<BooleanResultBase> Causes { get; }

    /// <summary>Gets the underlying <see cref="BooleanResultBase" />s that are the sources of the <see cref="Assertions" />.</summary>
    /// <remarks>Empty for a result with no causes — a leaf has nothing underlying it.</remarks>
    public IEnumerable<BooleanResultBase> UnderlyingAssertionSources =>
        _underlyingAssertionSources ??= PostOrderFold.Fold(
            this,
            CausalOperations,
            CombineCausalAssertionSources,
            ReadUnderlyingAssertionSources,
            WriteUnderlyingAssertionSources);

    /// <summary>
    /// Gets the underlying <see cref="BooleanResultBase" />s that are the sources of the
    /// <see cref="AllAssertions" /> — the "all" sibling of <see cref="UnderlyingAssertionSources" />,
    /// descending <see cref="Underlying" /> rather than <see cref="Causes" /> so that operands which
    /// did not determine the outcome are included too.
    /// </summary>
    /// <remarks>Empty for a result with no underlying results — a leaf has nothing underlying it.</remarks>
    public IEnumerable<BooleanResultBase> UnderlyingAllAssertionSources =>
        _underlyingAllAssertionSources ??= PostOrderFold.Fold(
            this,
            UnderlyingOperations,
            CombineAllAssertionSources,
            ReadUnderlyingAllAssertionSources,
            WriteUnderlyingAllAssertionSources);

    private BooleanResultBase[]? _underlyingAssertionSources;

    private BooleanResultBase[]? _underlyingAllAssertionSources;

    private static readonly Func<BooleanResultBase, IReadOnlyList<BooleanResultBase>> CausalOperations =
        result => Operations(result.Causes);

    private static readonly Func<BooleanResultBase, IReadOnlyList<BooleanResultBase>> UnderlyingOperations =
        result => Operations(result.Underlying);

    private static readonly Func<BooleanResultBase, IReadOnlyList<BooleanResultBase[]>, BooleanResultBase[]>
        CombineCausalAssertionSources = (result, foldedOperations) =>
            SourcesOf(result.Causes, foldedOperations);

    private static readonly Func<BooleanResultBase, IReadOnlyList<BooleanResultBase[]>, BooleanResultBase[]>
        CombineAllAssertionSources = (result, foldedOperations) =>
            SourcesOf(result.Underlying, foldedOperations);

    private static readonly Func<BooleanResultBase, BooleanResultBase[]?> ReadUnderlyingAssertionSources =
        result => result._underlyingAssertionSources;

    private static readonly Action<BooleanResultBase, BooleanResultBase[]> WriteUnderlyingAssertionSources =
        (result, sources) => result._underlyingAssertionSources = sources;

    private static readonly Func<BooleanResultBase, BooleanResultBase[]?> ReadUnderlyingAllAssertionSources =
        result => result._underlyingAllAssertionSources;

    private static readonly Action<BooleanResultBase, BooleanResultBase[]> WriteUnderlyingAllAssertionSources =
        (result, sources) => result._underlyingAllAssertionSources = sources;

    /// <summary>
    /// The children an assertion-source walk descends into. The walk deliberately stops at a child
    /// that is not itself an operation — that child <i>is</i> the source — so folding every child
    /// would turn a walk of the visited nodes into a walk of the whole tree.
    /// </summary>
    private protected static IReadOnlyList<TResult> Operations<TResult>(IEnumerable<TResult> children)
        where TResult : BooleanResultBase
    {
        List<TResult>? operations = null;

        foreach (var child in children)
            if (child is IBooleanOperationResult)
                (operations ??= []).Add(child);

        return operations ?? (IReadOnlyList<TResult>)[];
    }

    /// <summary>
    /// Concatenates, in child order, each descended child's sources and each child the walk stopped
    /// at. A result that contributed nothing has no sources: the answer is empty, not itself. Each
    /// source walk differs only in the children it is handed.
    /// </summary>
    /// <remarks>
    /// <paramref name="children" /> must be the same sequence <see cref="Operations{TResult}" /> was
    /// handed for this result: <paramref name="foldedOperations" /> is consumed positionally, so a
    /// walk whose two delegates disagree about which children they enumerate would misattribute
    /// sources rather than fail.
    /// <para>
    /// Ticket #188 removed a fallback to the result itself. It was reachable only from a leaf — every
    /// operation node has a causal operand, and both in-library consumers reach a walk only behind an
    /// <see cref="IBooleanOperationResult" /> guard — where it made the property contradict its own
    /// name and left an obvious fixpoint descent non-terminating.
    /// </para>
    /// </remarks>
    private protected static TResult[] SourcesOf<TResult>(
        IEnumerable<TResult> children,
        IReadOnlyList<TResult[]> foldedOperations)
        where TResult : BooleanResultBase
    {
        var sources = new List<TResult>();
        var nextOperation = 0;

        foreach (var child in children)
            if (child is IBooleanOperationResult)
                sources.AddRange(foldedOperations[nextOperation++]);
            else
                sources.Add(child);

        return sources.ToArray();
    }

    /// <summary>Gets the underlying <see cref="BooleanResultBase" /> that contribute to this result.</summary>
    public abstract IEnumerable<BooleanResultBase> Underlying { get; }

    /// <summary>Determines whether the <see cref="Satisfied" /> Property is equal to the specified boolean value.</summary>
    /// <param name="other">The boolean value to compare with the current BooleanResultBase object.</param>
    /// <returns>True if the current BooleanResultBase object is equal to the specified boolean value; otherwise, false.</returns>
    public bool Equals(bool other) => Satisfied == other;

    /// <summary>Determines whether the this object is equal to another BooleanResultBase object.</summary>
    /// <param name="other">The BooleanResultBase object to compare with the current object.</param>
    /// <returns>true if the current object is equal to the other object; otherwise, false.</returns>
    public bool Equals(BooleanResultBase? other) => Satisfied == other?.Satisfied;

    /// <summary>Returns a human-readable description of the tree of conditions that make up this result.</summary>
    /// <returns>A string that describes the tree of conditions that make up this result.</returns>
    public override string ToString() => Reason;

    /// <summary>Defines the true operator for the <see cref="BooleanResultBase{TMetadata}" /> class.</summary>
    /// <param name="result">The <see cref="BooleanResultBase{TMetadata}" /> instance.</param>
    /// <returns><c>true</c> if the <paramref name="result" /> is satisfied; otherwise, <c>false</c>.</returns>
    public static bool operator true(BooleanResultBase result) =>
        result.Satisfied;

    /// <summary>Defines the false operator for the <see cref="BooleanResultBase{TMetadata}" /> class.</summary>
    /// <param name="result">The <see cref="BooleanResultBase{TMetadata}" /> instance.</param>
    /// <returns><c>true</c> if the <paramref name="result" /> is not satisfied; otherwise, <c>false</c>.</returns>
    public static bool operator false(BooleanResultBase result) =>
        !result.Satisfied;

    /// <summary>Determines whether two <see cref="BooleanResultBase{TMetadata}" /> objects are equal.</summary>
    /// <param name="left">The first operand to compare.</param>
    /// <param name="right">The second operand to compare.</param>
    /// <returns><c>true</c> if the two objects are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(BooleanResultBase left, BooleanResultBase right) =>
        left.Equals(right);

    /// <summary>Determines whether two <see cref="BooleanResultBase{TMetadata}" /> objects are equal.</summary>
    /// <param name="left">The first operand to compare.</param>
    /// <param name="right">The second operand to compare.</param>
    /// <returns><c>true</c> if the two objects are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(bool left, BooleanResultBase right) =>
        left == right.Satisfied;

    /// <summary>
    /// Determines whether two <see cref="BooleanResultBase{TMetadata}" /> objects are not equal.
    /// </summary>
    /// <param name="left">The first operand to compare.</param>
    /// <param name="right">The second operand to compare.</param>
    /// <returns><c>true</c> if the two objects are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(bool left, BooleanResultBase right) => !(left == right);

    /// <summary>Determines whether two <see cref="BooleanResultBase{TMetadata}" /> objects are equal.</summary>
    /// <param name="left">The first operand to compare.</param>
    /// <param name="right">The second operand to compare.</param>
    /// <returns><c>true</c> if the two objects are equal; otherwise, <c>false</c>.</returns>
    public static bool operator ==(BooleanResultBase left, bool right) =>
        left.Satisfied == right;

    /// <summary>
    /// Determines whether two <see cref="BooleanResultBase{TMetadata}" /> objects are not equal.
    /// </summary>
    /// <param name="left">The first operand to compare.</param>
    /// <param name="right">The second operand to compare.</param>
    /// <returns><c>true</c> if the two objects are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(BooleanResultBase left, bool right) => !(left == right);

    /// <summary>Implements the inequality operator for comparing two instances of <see cref="BooleanResultBase{TMetadata}" />.</summary>
    /// <param name="left">The first operand to compare.</param>
    /// <param name="right">The second operand to compare.</param>
    /// <returns><c>true</c> if the two instances are not equal; otherwise, <c>false</c>.</returns>
    public static bool operator !=(BooleanResultBase left, BooleanResultBase right) =>
        !(left == right);

    /// <summary>Defines an implicit conversion from <see cref="BooleanResultBase{TMetadata}" /> to <see cref="bool" />.</summary>
    /// <param name="result">The <see cref="BooleanResultBase{TMetadata}" /> instance to convert.</param>
    /// <returns>The boolean value indicating whether the result is satisfied.</returns>
    public static implicit operator bool(BooleanResultBase result) =>
        result.Satisfied;

    /// <summary>
    /// Performs a logical AND operation between the current BooleanResultBase instance and another BooleanResultBase
    /// instance.
    /// </summary>
    /// <param name="right">The other boolean result instance to perform the logical AND operation with.</param>
    /// <returns>A new boolean result instance representing the result of the logical AND operation.</returns>
    public BooleanResultBase<string> And(BooleanResultBase right) =>
        new AndBooleanResult<string>(ToExplanationResult(), right.ToExplanationResult());

    /// <summary>
    /// Performs a conditional AND operation between the current BooleanResultBase instance and another
    /// BooleanResultBase instance. This will short-circuit the evaluation of the right operand if the left operand is not
    /// satisfied.
    /// </summary>
    /// <param name="right">The other boolean result instance to perform the logical AND operation with.</param>
    /// <returns>A new boolean result instance representing the result of the logical AND operation.</returns>
    public BooleanResultBase<string> AndAlso(BooleanResultBase right) => Satisfied
        ? new AndAlsoBooleanResult<string>(ToExplanationResult(), right.ToExplanationResult())
        : new AndAlsoBooleanResult<string>(ToExplanationResult());

    /// <summary>
    /// Performs a logical OR operation between the current BooleanResultBase instance and another BooleanResultBase
    /// instance.
    /// </summary>
    /// <param name="right">The other boolean result instance to perform the OR operation with.</param>
    /// <returns>A new boolean result instance representing the result of the OR operation.</returns>
    public BooleanResultBase<string> Or(BooleanResultBase right) =>
        new OrBooleanResult<string>(ToExplanationResult(), right.ToExplanationResult());

    /// <summary>
    /// Performs a conditional OR operation between the current BooleanResultBase instance and another
    /// BooleanResultBase instance. This will short-circuit the evaluation of the right operand if the left operand is
    /// satisfied.
    /// </summary>
    /// <param name="right">The other boolean result instance to perform the OR operation with.</param>
    /// <returns>A new boolean result instance representing the result of the OR operation.</returns>
    public BooleanResultBase<string> OrElse(BooleanResultBase right) => Satisfied
        ? new OrElseBooleanResult<string>(ToExplanationResult())
        : new OrElseBooleanResult<string>(ToExplanationResult(), right.ToExplanationResult());

    /// <summary>
    /// Performs a logical exclusive OR (XOR) operation between this BooleanResultBase instance and another
    /// BooleanResultBase instance.
    /// </summary>
    /// <param name="right">The other boolean result instance to perform the XOR operation with.</param>
    /// <returns>A new boolean result instance representing the result of the XOR operation.</returns>
    public BooleanResultBase<string> XOr(BooleanResultBase right) =>
        new XOrBooleanResult<string>(ToExplanationResult(), right.ToExplanationResult());

    /// <summary>Overloads the bitwise AND operator to perform a logical AND operation on two BooleanResultBase instances.</summary>
    /// <param name="left">The left boolean result instance.</param>
    /// <param name="right">The right boolean result instance.</param>
    /// <returns>A new BooleanResultBase instance representing the result of the logical AND operation.</returns>
    public static BooleanResultBase<string> operator &(
        BooleanResultBase left,
        BooleanResultBase right) =>
        left.And(right);

    /// <summary>Overloads the logical OR operator (|) to perform a logical OR operation on two BooleanResultBase instances.</summary>
    /// <param name="left">The left boolean result instance.</param>
    /// <param name="right">The right boolean result instance.</param>
    /// <returns>A new BooleanResultBase instance representing the result of the logical OR operation.</returns>
    public static BooleanResultBase<string> operator |(
        BooleanResultBase left,
        BooleanResultBase right) =>
        left.Or(right);

    /// <summary>Overloads the ^ operator to perform an exclusive OR (XOR) operation on two BooleanResultBase instances.</summary>
    /// <param name="left">The left boolean result operand.</param>
    /// <param name="right">The right boolean result operand.</param>
    /// <returns>A new BooleanResultBase instance representing the result of the XOR operation.</returns>
    public static BooleanResultBase<string> operator ^(
        BooleanResultBase left,
        BooleanResultBase right) =>
        left.XOr(right);

    /// <summary>Determines whether the current object is equal to another object.</summary>
    /// <param name="obj">The object to compare with the current object.</param>
    /// <returns>true if the specified object is equal to the current object; otherwise, false.</returns>
    public override bool Equals(object? obj) =>
        obj switch
        {
            null => false,
            bool b => Equals(b),
            BooleanResultBase r => Equals(r),
            _ => false
        };

    /// <summary>Computes the hash code for the current BooleanResultBase object.</summary>
    /// <returns>A hash code for the current object.</returns>
    public override int GetHashCode() => Satisfied.GetHashCode();

    internal BooleanResultBase<string> ToExplanationResult() => new ExplanationBooleanResult(this);
}

/// <summary>Represents a base class for boolean results with metadata.</summary>
/// <typeparam name="TMetadata">The type of the metadata associated with the boolean result.</typeparam>
public abstract class BooleanResultBase<TMetadata>
    : BooleanResultBase
{
    /// <summary>Prevent inheritance from outside of this project/assembly.</summary>
    internal BooleanResultBase()
    {
    }

    /// <summary>Gets the metadata/assertions yielded by results that caused the outcome.</summary>
    public IEnumerable<TMetadata> Values
    {
        get
        {
            ConstructMetadataTiers();
            return MetadataTier.Metadata;
        }
    }

    /// <summary>Gets the metadata yielded by all results that evaluated.</summary>
    /// <remarks>
    /// Materialised on the first read and answered from that array thereafter, as its two siblings
    /// <see cref="BooleanResultBase.RootAssertions" /> and <see cref="BooleanResultBase.AllRootAssertions" />
    /// already were — a result cannot change once evaluated, which is the premise all three rest on.
    /// It was the one root projection without a cache, so every read re-folded the tier tree that
    /// ticket #189 had just made the only reader of (ticket #193).
    /// <para>
    /// The alternative was to hoist the walk's memo onto <see cref="MetadataNode{TMetadata}" />, beside
    /// the <c>Underlying</c> and <c>Resolved</c> it already caches. That is sound but a worse trade:
    /// the memo holds an array per <i>node</i>, and over a fully-causal chain the tier tree's arrays
    /// sum to the square of the chain — retained for the lifetime of the result rather than dropped
    /// when the walk ends. Caching here retains one array per result that was actually asked, which is
    /// the trade the siblings already make.
    /// </para>
    /// </remarks>
    public IEnumerable<TMetadata> RootValues => field ??= MaterialiseRootValues();

    /// <remarks>
    /// The tiers are constructed here rather than in the property, so they are built on the read that
    /// needs them and not on every later one. <see cref="ConstructMetadataTiers" /> is idempotent and
    /// returns at once once this result's tier exists, so the difference is small — but a cached
    /// projection that still calls into a walk on every read is the shape this ticket is about.
    /// <para>
    /// The two verbs are not the same one: ticket #195 renamed <c>MaterialiseMetadataTiers</c> to
    /// <see cref="ConstructMetadataTiers" /> precisely because it stopped forcing what it built.
    /// Forcing is what this method is for, which is why it keeps the word the other gave up.
    /// </para>
    /// </remarks>
    private TMetadata[] MaterialiseRootValues()
    {
        ConstructMetadataTiers();

        return this.GetRootValues().ElseIfEmpty(Values).ToArray();
    }

    /// <summary>Constructs the metadata tier of every result beneath this one, deepest first.</summary>
    /// <remarks>
    /// It constructs them; it does not read their metadata. Constructing a tier is what is dangerous
    /// top-down: several of them read their source as they are built — a decorator's is its underlying
    /// result's <see cref="Values" /> — so building the root's first would build every tier beneath it
    /// in one nested chain, two frames per level. That is the same crash class as the assertion walks,
    /// reached through <see cref="Values" /> rather than through a traversal property, and
    /// <c>MetadataTierMaterialisationTests.Should_build_the_deepest_tier_at_a_constant_stack_depth_however_long_the_chain</c>
    /// is what holds it.
    /// <para>
    /// It used to force each tier's metadata as well. That is what made reading one level build every
    /// level beneath it, because a composition's tier was a lazy union over its causes' — quadratic in
    /// a fully-causal chain (ticket #195). <see cref="MetadataNode{TMetadata}.Metadata" /> now computes
    /// that union by walking rather than recursing, so no tier needs forcing here: each is built when
    /// something reads it, and nothing reads a level it was not asked for.
    /// </para>
    /// </remarks>
    private void ConstructMetadataTiers() =>
        PostOrderFold.Fold(this, CausesWithMetadata, ConstructTier, ReadConstructedTier, WriteConstructedTier);

    private MetadataNode<TMetadata>? _constructedTier;

    private static readonly Func<BooleanResultBase<TMetadata>, IReadOnlyList<BooleanResultBase<TMetadata>>>
        CausesWithMetadata = result => result.CausesWithValues.AsList();

    private static readonly Func<
            BooleanResultBase<TMetadata>,
            IReadOnlyList<MetadataNode<TMetadata>>,
            MetadataNode<TMetadata>>
        ConstructTier = (result, _) => result.MetadataTier;

    private static readonly Func<BooleanResultBase<TMetadata>, MetadataNode<TMetadata>?> ReadConstructedTier =
        result => result._constructedTier;

    private static readonly Action<BooleanResultBase<TMetadata>, MetadataNode<TMetadata>> WriteConstructedTier =
        (result, tier) => result._constructedTier = tier;

    /// <summary>Gets the metadata tree associated with this result.</summary>
    public abstract MetadataNode<TMetadata> MetadataTier { get; }

    /// <summary>Gets the underlying causes with metadata that contribute to this result.</summary>
    public abstract IEnumerable<BooleanResultBase<TMetadata>> CausesWithValues { get; }

    /// <summary>Gets the underlying <see cref="BooleanResultBase" />s that are the sources of the <see cref="Values" />.</summary>
    /// <remarks>
    /// Empty for a result with no causal values — a leaf has nothing underlying it, so its own
    /// values are reached through <see cref="Values" />, not through this property.
    /// </remarks>
    public IEnumerable<BooleanResultBase<TMetadata>> UnderlyingMetadataSources =>
        _underlyingMetadataSources ??= PostOrderFold.Fold(
            this,
            CausalValueOperations,
            CombineMetadataSources,
            ReadUnderlyingMetadataSources,
            WriteUnderlyingMetadataSources);

    private BooleanResultBase<TMetadata>[]? _underlyingMetadataSources;

    private static readonly Func<BooleanResultBase<TMetadata>, IReadOnlyList<BooleanResultBase<TMetadata>>>
        CausalValueOperations = result => Operations(result.CausesWithValues);

    private static readonly Func<
            BooleanResultBase<TMetadata>,
            IReadOnlyList<BooleanResultBase<TMetadata>[]>,
            BooleanResultBase<TMetadata>[]>
        CombineMetadataSources = (result, foldedOperations) =>
            SourcesOf(result.CausesWithValues, foldedOperations);

    private static readonly Func<BooleanResultBase<TMetadata>, BooleanResultBase<TMetadata>[]?>
        ReadUnderlyingMetadataSources = result => result._underlyingMetadataSources;

    private static readonly Action<BooleanResultBase<TMetadata>, BooleanResultBase<TMetadata>[]>
        WriteUnderlyingMetadataSources = (result, sources) => result._underlyingMetadataSources = sources;

    /// <summary>Gets the underlying boolean results with metadata that contribute to this result.</summary>
    public abstract IEnumerable<BooleanResultBase<TMetadata>> UnderlyingWithValues { get; }

    /// <summary>
    /// Performs a logical AND operation between the current BooleanResultBase instance and another BooleanResultBase
    /// instance.
    /// </summary>
    /// <param name="right">The other boolean result instance to perform the logical AND operation with.</param>
    /// <returns>A new boolean result instance representing the result of the logical AND operation.</returns>
    public BooleanResultBase<TMetadata> And(BooleanResultBase<TMetadata> right) =>
        new AndBooleanResult<TMetadata>(this, right);

    /// <summary>
    /// Performs a conditional AND operation between the current BooleanResultBase instance and another
    /// BooleanResultBase instance. This will short-circuit the evaluation of the right operand if the left operand is not
    /// satisfied.
    /// </summary>
    /// <param name="right">The other boolean result instance to perform the logical AND operation with.</param>
    /// <returns>A new boolean result instance representing the result of the logical AND operation.</returns>
    public BooleanResultBase<TMetadata> AndAlso(BooleanResultBase<TMetadata> right) => Satisfied
        ? new AndAlsoBooleanResult<TMetadata>(this, right)
        : new AndAlsoBooleanResult<TMetadata>(this);

    /// <summary>
    /// Performs a logical OR operation between the current BooleanResultBase instance and another BooleanResultBase
    /// instance.
    /// </summary>
    /// <param name="right">The other boolean result instance to perform the OR operation with.</param>
    /// <returns>A new boolean result instance representing the result of the OR operation.</returns>
    public BooleanResultBase<TMetadata> Or(BooleanResultBase<TMetadata> right) =>
        new OrBooleanResult<TMetadata>(this, right);

    /// <summary>
    /// Performs a conditional OR operation between the current BooleanResultBase instance and another
    /// BooleanResultBase instance. This will short-circuit the evaluation of the right operand if the left operand is
    /// satisfied.
    /// </summary>
    /// <param name="right">The other boolean result instance to perform the OR operation with.</param>
    /// <returns>A new boolean result instance representing the result of the OR operation.</returns>
    public BooleanResultBase<TMetadata> OrElse(BooleanResultBase<TMetadata> right) => Satisfied
        ? new OrElseBooleanResult<TMetadata>(this)
        : new OrElseBooleanResult<TMetadata>(this, right);

    /// <summary>
    /// Performs a logical exclusive OR (XOR) operation between this BooleanResultBase instance and another
    /// BooleanResultBase instance.
    /// </summary>
    /// <param name="right">The other boolean result instance to perform the XOR operation with.</param>
    /// <returns>A new boolean result instance representing the result of the XOR operation.</returns>
    public BooleanResultBase<TMetadata> XOr(BooleanResultBase<TMetadata> right) =>
        new XOrBooleanResult<TMetadata>(this, right);

    /// <summary>
    /// Returns a new instance of <see cref="NotBooleanOperationResult{TMetadata}" /> that represents the logical negation of
    /// the current instance.
    /// </summary>
    /// <returns>
    /// A new instance of <see cref="NotBooleanOperationResult{TMetadata}" /> that represents the logical negation of the
    /// current instance.
    /// </returns>
    public BooleanResultBase<TMetadata> Not() => new NotBooleanOperationResult<TMetadata>(this);

    /// <summary>Overloads the bitwise AND operator to perform a logical AND operation on two BooleanResultBase instances.</summary>
    /// <param name="left">The left boolean result instance.</param>
    /// <param name="right">The right boolean result instance.</param>
    /// <returns>A new BooleanResultBase instance representing the result of the logical AND operation.</returns>
    public static BooleanResultBase<TMetadata> operator &(
        BooleanResultBase<TMetadata> left,
        BooleanResultBase<TMetadata> right) =>
        left.And(right);

    /// <summary>Overloads the logical OR operator (|) to perform a logical OR operation on two BooleanResultBase instances.</summary>
    /// <param name="left">The left boolean result instance.</param>
    /// <param name="right">The right boolean result instance.</param>
    /// <returns>A new BooleanResultBase instance representing the result of the logical OR operation.</returns>
    public static BooleanResultBase<TMetadata> operator |(
        BooleanResultBase<TMetadata> left,
        BooleanResultBase<TMetadata> right) =>
        left.Or(right);

    /// <summary>Overloads the ^ operator to perform an exclusive OR (XOR) operation on two BooleanResultBase instances.</summary>
    /// <param name="left">The left boolean result operand.</param>
    /// <param name="right">The right boolean result operand.</param>
    /// <returns>A new BooleanResultBase instance representing the result of the XOR operation.</returns>
    public static BooleanResultBase<TMetadata> operator ^(
        BooleanResultBase<TMetadata> left,
        BooleanResultBase<TMetadata> right) =>
        left.XOr(right);

    /// <summary>Overloads the logical NOT operator for the BooleanResultBase class.</summary>
    /// <param name="result">The boolean result object to negate.</param>
    /// <returns>A new boolean result object that represents the negation of the input.</returns>
    public static BooleanResultBase<TMetadata> operator !(
        BooleanResultBase<TMetadata> result) =>
        result.Not();
}
