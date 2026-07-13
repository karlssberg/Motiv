using Motiv.And;
using Motiv.AndAlso;
using Motiv.ChangeModelType;
using Motiv.MetadataToExplanationAdapter;
using Motiv.Not;
using Motiv.Or;
using Motiv.OrElse;
using Motiv.SyncToAsyncAdapter;
using Motiv.XOr;

namespace Motiv;

/// <summary>
/// The generic-less base class for all specifications. It ensures that all specifications have a description and
/// a statement, without requiring knowledge of the model type.
/// </summary>
public abstract class SpecBase
{
    /// <summary>Prevents the external instantiation of the <see cref="SpecBase" /> class.</summary>
    internal SpecBase()
    {
    }

    /// <summary>Gets a description of the specification.  This is used for debugging/logging purposes.</summary>
    public abstract ISpecDescription Description { get; }

    /// <summary>Gets the propositional statement.</summary>
    public string Name => Description.Statement;

    /// <summary>Gets the propositional statement.</summary>
    public string Expression => Description.Detailed;

    /// <summary>Gets the underlying specifications that make up this composite proposition.</summary>
    /// <remarks>This will yield an empty collection if the specification is dynamically generated at evaluation-time.</remarks>
    public abstract IEnumerable<SpecBase> Underlying { get; }
}

/// <summary>
/// The base class for all specifications. At its most basic, a 'Spec' is an encapsulated predicate function that
/// can be evaluated against a model.  When the predicate is evaluated, it returns a result that contains the Boolean
/// result of the predicate as well as metadata that captures the meaning behind the predicate.  By encapsulating the
/// predicate we can supply methods to assist with combining specifications together to form more complex specifications,
/// which together ultimately model the desired logical proposition.
/// </summary>
/// <typeparam name="TModel">The model type that the specification will evaluate against</typeparam>
public abstract class SpecBase<TModel> : SpecBase
{
    /// <summary>Prevents the external instantiation of the <see cref="SpecBase{TModel}" /> class.</summary>
    internal SpecBase()
    {
    }

    /// <summary>
    /// Evaluates the proposition against the model and returns a boolean indicating whether it is satisfied,
    /// without allocating result objects.
    /// </summary>
    /// <param name="model">The model to evaluate the specification against.</param>
    /// <returns><c>true</c> if the model satisfies the proposition; otherwise, <c>false</c>.</returns>
    public virtual bool Matches(TModel model) => Evaluate(model).Satisfied;

    /// <summary>
    /// Evaluates the proposition against the model and returns a result that contains the Boolean result of the
    /// predicate and an explanation of the result.
    /// </summary>
    /// <param name="model">The model to evaluate the specification against.</param>
    /// <returns>A result that contains the Boolean result of the predicate and an explanation of the result.</returns>
    public BooleanResultBase<string> Evaluate(TModel model) =>
        this switch
        {
            SpecBase<TModel, string> explanationSpec => explanationSpec.Evaluate(model),
            _ => ToExplanationSpec().Evaluate(model)
        };

    /// <inheritdoc cref="Evaluate(TModel)"/>
    [Obsolete("Use Evaluate instead.")]
    public BooleanResultBase<string> IsSatisfiedBy(TModel model) => Evaluate(model);

    /// <summary>
    /// Converts this specification to an explanation specification (i.e., Spec&lt;TModel, string&gt;). This is
    /// necessary when establishing a "lowest-common-denominator" between very different specification. Therefore,
    /// specifications with different metadata types will be wrapped in a spec that uses string as the metadata type.
    /// </summary>
    /// <returns></returns>
    public abstract SpecBase<TModel, string> ToExplanationSpec();

    /// <summary>Combines this specification with another specification using the logical AND operator.</summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical AND of this specification and the other specification.</returns>
    public SpecBase<TModel, string> And(SpecBase<TModel> spec) =>
        new AndSpec<TModel, string>(ToExplanationSpec(), spec.ToExplanationSpec());

    /// <summary>
    /// Combines this specification with another specification using the conditional AND operator. The right operand
    /// is only evaluated if the left operand evaluates to <c>true</c>. This is commonly referred to as
    /// "short-circuiting".
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the conditional AND of this specification and the other specification.</returns>
    public SpecBase<TModel, string> AndAlso(SpecBase<TModel> spec) =>
        new AndAlsoSpec<TModel, string>(ToExplanationSpec(), spec.ToExplanationSpec());

    /// <summary>Combines this specification with another specification using the logical OR operator.</summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical OR of this specification and the other specification.</returns>
    public SpecBase<TModel, string> Or(SpecBase<TModel> spec) =>
        new OrSpec<TModel, string>(ToExplanationSpec(), spec.ToExplanationSpec());

    /// <summary>
    /// Combines this specification with another specification using the conditional OR operator. The right operand
    /// is only evaluated if the left operand evaluates to <c>false</c>. This is commonly referred to as
    /// "short-circuiting".
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the conditional OR of this specification and the other specification.</returns>
    public SpecBase<TModel, string> OrElse(SpecBase<TModel> spec) =>
        new OrElseSpec<TModel, string>(ToExplanationSpec(), spec.ToExplanationSpec());

    /// <summary>Combines this specification with another specification using the logical XOR operator.</summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical XOR of this specification and the other specification.</returns>
    public SpecBase<TModel, string> XOr(SpecBase<TModel> spec) =>
        new XOrSpec<TModel, string>(ToExplanationSpec(), spec.ToExplanationSpec());

    /// <summary>Negates this specification.</summary>
    /// <returns>A new specification that represents the logical NOT of this specification.</returns>
    public SpecBase<TModel, string> Not() =>
        new NotSpec<TModel, string>(ToExplanationSpec());

    /// <summary>Serializes the logical hierarchy of the specification to a string.</summary>
    /// <returns>A string that represents the logical hierarchy of the specification.</returns>
    public override string ToString() => Description.Statement;

    /// <summary>Combines two specifications using the logical AND operator.</summary>
    /// <param name="left">The left operand of the AND operation.</param>
    /// <param name="right">The right operand of the AND operation.</param>
    /// <returns>A new specification that represents the logical AND of the two specifications.</returns>
    public static SpecBase<TModel, string> operator &(
        SpecBase<TModel> left,
        SpecBase<TModel> right) =>
        left.And(right);

    /// <summary>Combines two specifications using the logical OR operator.</summary>
    /// <param name="left">The left operand of the OR operation.</param>
    /// <param name="right">The right operand of the OR operation.</param>
    /// <returns>A new specification that represents the logical OR of the two specifications.</returns>
    public static SpecBase<TModel, string> operator |(
        SpecBase<TModel> left,
        SpecBase<TModel> right) =>
        left.Or(right);

    /// <summary>Combines two specifications using the logical XOR operator.</summary>
    /// <param name="left">The left operand of the XOR operation.</param>
    /// <param name="right">The right operand of the XOR operation.</param>
    /// <returns>A new specification that represents the logical XOR of the two specifications.</returns>
    public static SpecBase<TModel, string> operator ^(
        SpecBase<TModel> left,
        SpecBase<TModel> right) =>
        left.XOr(right);

    /// <summary>Negates a specification.</summary>
    /// <param name="spec">The specification to negate.</param>
    /// <returns>A new specification that represents the logical NOT of the specification.</returns>
    public static SpecBase<TModel> operator !(
        SpecBase<TModel> spec) =>
        spec.Not();

    /// <summary>
    /// Converts the expression into a predicate function that can be used to evaluate the proposition against a model.
    /// </summary>
    /// <param name="spec">The specification to be converted into predicate</param>
    /// <returns>A predicate function</returns>
    public static implicit operator Func<TModel, bool>(SpecBase<TModel> spec) =>
        spec.Matches;
}

/// <summary>
/// The base class for all specifications. A specification is an encapsulated predicate that can be evaluated
/// against a model.  When the predicate is evaluated, it returns a result that contains the Boolean result of the
/// predicate as well as metadata that captures the meaning behind the predicate.  By encapsulating the predicate, we can
/// supply methods to assist with combining specifications together to form more complex specifications.
/// </summary>
/// <typeparam name="TModel">The model type that the specification will evaluate against</typeparam>
/// <typeparam name="TMetadata">The type of the metadata to associate with the predicate</typeparam>
public abstract class SpecBase<TModel, TMetadata> : SpecBase<TModel>
{
    private SpecBase<TModel, string>? _explanationSpec;
    private AsyncSpecBase<TModel, TMetadata>? _asyncSpec;

    /// <summary>Prevents the external instantiation of the <see cref="SpecBase{TModel,TMetadata}" /> class.</summary>
    internal SpecBase()
    {
    }

    /// <inheritdoc />
    public override bool Matches(TModel model) => EvaluateSpec(model).Satisfied;

    /// <summary>
    /// Evaluates the proposition against the model and returns a result that contains the Boolean result of the
    /// predicate in addition to the metadata.
    /// </summary>
    /// <param name="model">The model to evaluate the specification against.</param>
    /// <returns>A result that contains the Boolean result of the predicate in addition to the metadata.</returns>
    public new BooleanResultBase<TMetadata> Evaluate(TModel model) => EvaluateSpec(model);

    /// <inheritdoc cref="Evaluate(TModel)"/>
    [Obsolete("Use Evaluate instead.")]
    public new BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model) => Evaluate(model);

    /// <summary>
    /// Combines this specification with another specification using the logical AND operator. Both operands will be
    /// evaluated, regardless of whether the left operand evaluated to <c>false</c>
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical AND of this specification and the other specification.</returns>
    public SpecBase<TModel, TMetadata> And(SpecBase<TModel, TMetadata> spec) =>
        new AndSpec<TModel, TMetadata>(this, spec);

    /// <summary>
    /// Combines this specification with another specification using the conditional AND operator. The right operand
    /// is only evaluated if the left operand resolves to <c>true</c>, since a <c>false</c> left operand means the
    /// AND operation cannot return <c>true</c>. This is commonly referred to as "short-circuiting".
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the conditional AND of this specification and the other specification.</returns>
    public SpecBase<TModel, TMetadata> AndAlso(SpecBase<TModel, TMetadata> spec) =>
        new AndAlsoSpec<TModel, TMetadata>(this, spec);

    /// <summary>Combines this specification with another specification using the logical OR operator.</summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical OR of this specification and the other specification.</returns>
    public SpecBase<TModel, TMetadata> Or(SpecBase<TModel, TMetadata> spec) =>
        new OrSpec<TModel, TMetadata>(this, spec);

    /// <summary>
    /// Combines this specification with another specification using the conditional OR operator. The right operand
    /// is only evaluated if the left operand resolves to <c>false</c>, since a <c>true</c> left operand means the
    /// OR operation is already satisfied. This is commonly referred to as "short-circuiting".
    /// </summary>
    /// <param name="spec">The right operand.</param>
    /// <returns>A new specification that represents the conditional OR of this specification and the other specification.</returns>
    public SpecBase<TModel, TMetadata> OrElse(SpecBase<TModel, TMetadata> spec) =>
        new OrElseSpec<TModel, TMetadata>(this, spec);

    /// <summary>Combines this specification with another specification using the logical XOR operator.</summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical XOR of this specification and the other specification.</returns>
    public SpecBase<TModel, TMetadata> XOr(SpecBase<TModel, TMetadata> spec) =>
        new XOrSpec<TModel, TMetadata>(this, spec);

    /// <summary>Negates this specification.</summary>
    /// <returns>A new specification that represents the logical NOT of this specification.</returns>
    public new SpecBase<TModel, TMetadata> Not() =>
        new NotSpec<TModel, TMetadata>(this);

    /// <summary>Changes the <typeparamref name="TModel" /> <see cref="Type" /> of the specification.</summary>
    /// <param name="childModelSelector">
    /// A function that takes the model and returns the child model to evaluate the
    /// specification against.
    /// </param>
    /// <typeparam name="TNewModel"></typeparam>
    /// <returns>
    /// A new specification that represents the same specification but with a different <typeparamref name="TModel" />
    /// .
    /// </returns>
    public SpecBase<TNewModel, TMetadata> ChangeModelTo<TNewModel>(
        Func<TNewModel, TModel> childModelSelector) =>
        new ChangeModelTypeSpec<TNewModel, TModel, TMetadata>(
            this,
            childModelSelector.ThrowIfNull(nameof(childModelSelector)));

    /// <summary>Changes the <typeparamref name="TModel" /> <see cref="Type" /> of the specification.</summary>
    /// <typeparam name="TDerivedModel">
    /// The type to change the <typeparamref name="TModel" /> to. This type must be a subclass
    /// of <typeparamref name="TModel" />.
    /// </typeparam>
    /// <returns>
    /// A new specification that represents the same specification but with a different <typeparamref name="TModel" />
    /// .
    /// </returns>
    public SpecBase<TDerivedModel, TMetadata> ChangeModelTo<TDerivedModel>()
        where TDerivedModel : TModel =>
        new ChangeModelTypeSpec<TDerivedModel, TModel, TMetadata>(this, model => model);

    /// <summary>
    /// Converts this specification to an explanation specification (i.e., Spec&lt;TModel, string&gt;). This is
    /// necessary when establishing a "lowest-common-denominator" between very different specification. Therefore,
    /// specifications with different metadata types will be wrapped in a spec that uses string as the metadata type.
    /// </summary>
    /// <returns></returns>
    public override SpecBase<TModel, string> ToExplanationSpec() =>
        this switch
        {
            SpecBase<TModel, string> explanationSpec => explanationSpec,
            _ => _explanationSpec ??= new MetadataToExplanationAdapterSpec<TModel, TMetadata>(this)
        };

    /// <summary>
    /// Lifts this synchronous specification into the asynchronous specification hierarchy so that it can be
    /// composed with asynchronous specifications. Evaluation remains fully synchronous internally; results
    /// are identical to those produced by <see cref="Evaluate(TModel)" />.
    /// </summary>
    /// <returns>An asynchronous view over this specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> ToAsyncSpec() => _asyncSpec ??= CreateAsyncAdapter();

    /// <summary>Creates the adapter used by <see cref="ToAsyncSpec" />. Policies override this to preserve policy semantics.</summary>
    /// <returns>An asynchronous adapter for this specification.</returns>
    private protected virtual AsyncSpecBase<TModel, TMetadata> CreateAsyncAdapter() =>
        new SyncSpecAsyncAdapter<TModel, TMetadata>(this);

    /// <summary>
    /// Combines this specification with an asynchronous specification using the logical AND operator. This
    /// specification is lifted into the asynchronous hierarchy via <see cref="ToAsyncSpec" />. Both operands
    /// are evaluated sequentially (left, then right), regardless of the left operand's outcome.
    /// </summary>
    /// <param name="spec">The asynchronous specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical AND of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> And(AsyncSpecBase<TModel, TMetadata> spec) => ToAsyncSpec().And(spec);

    /// <summary>
    /// Combines this specification with an asynchronous specification using the conditional AND operator. This
    /// specification is lifted into the asynchronous hierarchy via <see cref="ToAsyncSpec" />. The right
    /// operand is only evaluated if the left operand resolves to <c>true</c> — for asynchronous specifications
    /// this means the right operand's work (including any I/O) is never started.
    /// </summary>
    /// <param name="spec">The asynchronous specification to combine with this specification.</param>
    /// <returns>A new specification that represents the conditional AND of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> AndAlso(AsyncSpecBase<TModel, TMetadata> spec) => ToAsyncSpec().AndAlso(spec);

    /// <summary>
    /// Combines this specification with an asynchronous specification using the logical OR operator. This
    /// specification is lifted into the asynchronous hierarchy via <see cref="ToAsyncSpec" />.
    /// </summary>
    /// <param name="spec">The asynchronous specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical OR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> Or(AsyncSpecBase<TModel, TMetadata> spec) => ToAsyncSpec().Or(spec);

    /// <summary>
    /// Combines this specification with an asynchronous specification using the conditional OR operator. This
    /// specification is lifted into the asynchronous hierarchy via <see cref="ToAsyncSpec" />. The right
    /// operand is only evaluated if the left operand resolves to <c>false</c>, since a <c>true</c> left
    /// operand means the OR operation is already satisfied — for asynchronous specifications this means the
    /// right operand's work (including any I/O) is never started.
    /// </summary>
    /// <param name="spec">The asynchronous right operand.</param>
    /// <returns>A new specification that represents the conditional OR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> OrElse(AsyncSpecBase<TModel, TMetadata> spec) => ToAsyncSpec().OrElse(spec);

    /// <summary>
    /// Combines this specification with an asynchronous specification using the logical XOR operator. This
    /// specification is lifted into the asynchronous hierarchy via <see cref="ToAsyncSpec" />. Both operands
    /// are evaluated sequentially (left, then right), regardless of the outcome.
    /// </summary>
    /// <param name="spec">The asynchronous specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical XOR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> XOr(AsyncSpecBase<TModel, TMetadata> spec) => ToAsyncSpec().XOr(spec);

    /// <summary>
    /// Combines this specification with an asynchronous specification using the logical AND operator,
    /// evaluating both operands concurrently. This specification is lifted into the asynchronous hierarchy via
    /// <see cref="ToAsyncSpec" />. The result is indistinguishable from <see cref="And(AsyncSpecBase{TModel,TMetadata})" /> —
    /// the reason, assertions, and justification are identical; only the evaluation strategy differs. Only use
    /// this when both operands' predicates are safe to execute concurrently (e.g. they do not share a
    /// non-thread-safe dependency such as an EF Core DbContext).
    /// </summary>
    /// <param name="spec">The asynchronous specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical AND of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> AndConcurrently(AsyncSpecBase<TModel, TMetadata> spec) => ToAsyncSpec().AndConcurrently(spec);

    /// <summary>
    /// Combines this specification with an asynchronous specification using the logical OR operator,
    /// evaluating both operands concurrently. This specification is lifted into the asynchronous hierarchy via
    /// <see cref="ToAsyncSpec" />. The result is indistinguishable from <see cref="Or(AsyncSpecBase{TModel,TMetadata})" /> —
    /// the reason, assertions, and justification are identical; only the evaluation strategy differs. Only use
    /// this when both operands' predicates are safe to execute concurrently (e.g. they do not share a
    /// non-thread-safe dependency such as an EF Core DbContext).
    /// </summary>
    /// <param name="spec">The asynchronous specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical OR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> OrConcurrently(AsyncSpecBase<TModel, TMetadata> spec) => ToAsyncSpec().OrConcurrently(spec);

    /// <summary>
    /// Combines this specification with an asynchronous specification using the logical XOR operator,
    /// evaluating both operands concurrently. This specification is lifted into the asynchronous hierarchy via
    /// <see cref="ToAsyncSpec" />. The result is indistinguishable from <see cref="XOr(AsyncSpecBase{TModel,TMetadata})" /> —
    /// the reason, assertions, and justification are identical; only the evaluation strategy differs. Only use
    /// this when both operands' predicates are safe to execute concurrently (e.g. they do not share a
    /// non-thread-safe dependency such as an EF Core DbContext).
    /// </summary>
    /// <param name="spec">The asynchronous specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical XOR of this specification and the other specification.</returns>
    public AsyncSpecBase<TModel, TMetadata> XOrConcurrently(AsyncSpecBase<TModel, TMetadata> spec) => ToAsyncSpec().XOrConcurrently(spec);

    /// <summary>Serializes the logical hierarchy of the specification to a string.</summary>
    /// <returns>A string that represents the logical hierarchy of the specification.</returns>
    public override string ToString() => Description.Statement;

    /// <summary>Combines two specifications using the logical AND operator.</summary>
    /// <param name="left">The left operand of the AND operation.</param>
    /// <param name="right">The right operand of the AND operation.</param>
    /// <returns>A new specification that represents the logical AND of the two specifications.</returns>
    public static SpecBase<TModel, TMetadata> operator &(
        SpecBase<TModel, TMetadata> left,
        SpecBase<TModel, TMetadata> right) =>
        left.And(right);

    /// <summary>Combines two specifications using the logical OR operator.</summary>
    /// <param name="left">The left operand of the OR operation.</param>
    /// <param name="right">The right operand of the OR operation.</param>
    /// <returns>A new specification that represents the logical OR of the two specifications.</returns>
    public static SpecBase<TModel, TMetadata> operator |(
        SpecBase<TModel, TMetadata> left,
        SpecBase<TModel, TMetadata> right) =>
        left.Or(right);

    /// <summary>Combines two specifications using the logical XOR operator.</summary>
    /// <param name="left">The left operand of the XOR operation.</param>
    /// <param name="right">The right operand of the XOR operation.</param>
    /// <returns>A new specification that represents the logical XOR of the two specifications.</returns>
    public static SpecBase<TModel, TMetadata> operator ^(
        SpecBase<TModel, TMetadata> left,
        SpecBase<TModel, TMetadata> right) =>
        left.XOr(right);

    /// <summary>Negates a specification.</summary>
    /// <param name="spec">The specification to negate.</param>
    /// <returns>A new specification that represents the logical NOT of the specification.</returns>
    public static SpecBase<TModel, TMetadata> operator !(
        SpecBase<TModel, TMetadata> spec) =>
        spec.Not();

    /// <summary>
    /// Evaluates the specification against the model and returns a result that contains the Boolean result of the
    /// predicate in addition to the metadata.
    /// </summary>
    /// <param name="model">The model to evaluate the specification against.</param>
    /// <returns>A result that contains the Boolean result of the predicate in addition to the metadata.</returns>
    protected abstract BooleanResultBase<TMetadata> EvaluateSpec(TModel model);
}
