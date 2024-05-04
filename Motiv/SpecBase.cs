using Motiv.And;
using Motiv.AndAlso;
using Motiv.ChangeModelType;
using Motiv.MetadataToExplanationAdapter;
using Motiv.Not;
using Motiv.Or;
using Motiv.OrElse;
using Motiv.XOr;

namespace Motiv;

/// <summary>
/// The generic-less base class for all specifications. It ensures that all specifications have a description and a
/// statement, without requiring knowledge of the model type.
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
    public string Statement => Description.Statement;
    
    /// <summary>Gets the propositional statement.</summary>
    public string Expression => Description.Detailed;
    
    /// <summary>
    /// Gets the underlying specifications that make up this composite proposition.
    /// </summary>
    /// <remarks>
    /// This will yield an empty collection if the specification is dynamically generated at evaluation-time.
    /// </remarks>
    public abstract IEnumerable<SpecBase> Underlying { get; }
}

/// <summary>
/// The base class for all specifications. At its most basic, a 'Spec' is an encapsulated predicate function that can
/// be evaluated against a model.  When the predicate is evaluated, it returns a result that contains the Boolean
/// result of the predicate as well as metadata that captures the meaning behind the predicate.  By encapsulating the
/// predicate we can supply methods to assist with combining specifications together to form more complex
/// specifications, which together ultimately model the desired logical proposition.
/// </summary>
/// <typeparam name="TModel">The model type that the specification will evaluate against</typeparam>
public abstract class SpecBase<TModel> : SpecBase
{
    /// <summary>Prevents the external instantiation of the <see cref="SpecBase{TModel}" /> class.</summary>
    internal SpecBase()
    {
    }
    
    /// <summary>
    /// Converts this specification to an explanation specification (i.e., Spec&lt;TModel, string&gt;).
    /// This is necessary when establishing a "lowest-common-denominator" between very different specification.
    /// Therefore, specifications with different metadata types will be wrapped in a spec that uses string as the
    /// metadata type.
    /// </summary>
    /// <returns></returns>
    public abstract SpecBase<TModel, string> ToExplanationSpec();

    /// <summary>Combines this specification with another specification using the logical AND operator.</summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical AND of this specification and the other specification.</returns>
    public SpecBase<TModel, string> And(SpecBase<TModel> spec) =>
        new AndSpec<TModel, string>(ToExplanationSpec(), spec.ToExplanationSpec());

    /// <summary>
    /// Combines this specification with another specification using the conditional AND operator.
    /// The left operand will only be evaluated only if the right operand evaluates to <c>true</c>.
    /// This is commonly referred to as "short-circuiting".
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>
    /// A new specification that represents the conditional AND of this specification and the other specification.
    /// </returns>
    public SpecBase<TModel, string> AndAlso(SpecBase<TModel> spec) =>
        new AndAlsoSpec<TModel, string>(ToExplanationSpec(), spec.ToExplanationSpec());

    /// <summary>Combines this specification with another specification using the logical OR operator.</summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical OR of this specification and the other specification.</returns>
    public SpecBase<TModel, string> Or(SpecBase<TModel> spec) =>
        new OrSpec<TModel, string>(ToExplanationSpec(), spec.ToExplanationSpec());

    /// <summary>
    /// Combines this specification with another specification using the conditional OR operator.
    /// The left operand will only be evaluated only if the right operand evaluates to <c>false</c>.
    /// This is commonly referred to as "short-circuiting".
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>
    /// A new specification that represents the conditional OR of this specification and the other specification.
    /// </returns>
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
    public static SpecBase<TModel, string> operator !(
        SpecBase<TModel> spec) =>
        spec.Not();
}

/// <summary>
/// The base class for all specifications. A specification is an encapsulated predicate that can be evaluated
/// against a model.  When the predicate is evaluated, it returns a result that contains the Boolean result of the
/// predicate as well as metadata that captures the meaning behind the predicate.  By encapsulating the predicate we can
/// supply methods to assist with combining specifications together to form more complex specifications.
/// </summary>
/// <typeparam name="TModel">The model type that the specification will evaluate against</typeparam>
/// <typeparam name="TMetadata">The type of the metadata to associate with the predicate</typeparam>
public abstract class SpecBase<TModel, TMetadata> : SpecBase<TModel>
{
    /// <summary>Prevents the external instantiation of the <see cref="SpecBase{TModel,TMetadata}" /> class.</summary>
    internal SpecBase()
    {
    }

    /// <summary>
    /// Evaluates the specification against the model and returns a result that contains the Boolean result of the
    /// predicate in addition to the metadata.
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public abstract BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model);

    /// <summary>
    /// Combines this specification with another specification using the logical AND operator. Both operands will be
    /// evaluated, regardless of whether the left operand evaluated to <c>false</c>
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical AND of this specification and the other specification.</returns>
    public SpecBase<TModel, TMetadata> And(SpecBase<TModel, TMetadata> spec) =>
        new AndSpec<TModel, TMetadata>(this, spec);

    /// <summary>
    /// Combines this specification with another specification using the logical AND operator. The operands are
    /// short-circuted, meaning that if the left operand resolves to <c>false</c> then it is not possible for the AND
    /// operation to return <c>true</c>.  Therefore the compiler does not bother evaluating the right operand.
    /// </summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical AND of this specification and the other specification.</returns>
    public SpecBase<TModel, TMetadata> AndAlso(SpecBase<TModel, TMetadata> spec) =>
        new AndAlsoSpec<TModel, TMetadata>(this, spec);

    /// <summary>Combines this specification with another specification using the logical OR operator.</summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical OR of this specification and the other specification.</returns>
    public SpecBase<TModel, TMetadata> Or(SpecBase<TModel, TMetadata> spec) =>
        new OrSpec<TModel, TMetadata>(this, spec);

    /// <summary>
    /// Combines this specification with another specification using the logical OR operator. The operands are
    /// short-circuted, meaning that if the left operand resolves to <c>false</c> then it is not possible for the OR
    /// operation to return <c>true</c>.  Therefore the compiler does not bother evaluating the right operand.
    /// </summary>
    /// <param name="spec">The right operand.</param>
    /// <returns>A new specification that represents the logical PR of this specification and the other specification.</returns>
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
    /// Converts this specification to an explanation specification (i.e., Spec&lt;TModel, string&gt;).
    /// This is necessary when establishing a "lowest-common-denominator" between very different specification.
    /// Therefore, specifications with different metadata types will be wrapped in a spec that uses string as the
    /// metadata type.
    /// </summary>
    /// <returns></returns>
    public override SpecBase<TModel, string> ToExplanationSpec() =>
        this switch
        {
            SpecBase<TModel, string> explanationSpec => explanationSpec,
            _ => new MetadataToExplanationAdapterSpec<TModel, TMetadata>(this)
        };

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
}