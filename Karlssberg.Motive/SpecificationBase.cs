using Karlssberg.Motive.And;
using Karlssberg.Motive.ChangeModelType;
using Karlssberg.Motive.Not;
using Karlssberg.Motive.Or;
using Karlssberg.Motive.XOr;

namespace Karlssberg.Motive;

/// <summary>
///     The base class for all specifications.
///     A specification is an encapsulated predicate that can be evaluated against a model.  When the predicate is
///     evaluated, it returns a result that contains
///     the Boolean result of the predicate as well as metadata that captures the meaning behind the predicate.  By
///     encapsulating the predicate we can supply
///     methods to assist with combining specifications together to form more complex specifications.
/// </summary>
/// <typeparam name="TModel">The model type that the specification will evaluate against</typeparam>
/// <typeparam name="TMetadata">The type of the metadata to associate with the predicate</typeparam>
public abstract class SpecificationBase<TModel, TMetadata>
{
    internal SpecificationBase()
    {
    }

    /// <summary>
    ///     The description of the specification.  This is used for debugging/logging purposes.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    ///     Evaluates the specification against the model and returns a result that contains the Boolean result of the
    ///     predicate in addition to the metadata.
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public abstract BooleanResultBase<TMetadata> Evaluate(TModel model);

    /// <summary>
    ///     Evaluates the specification against the model and returns a Boolean result.
    /// </summary>
    /// <param name="model">
    ///     The model to evaluate the specification against.
    /// </param>
    /// <returns>
    ///     The Boolean result of the specification.
    /// </returns>
    public bool IsSatisfiedBy(TModel model) => Evaluate(model).IsSatisfied;

    /// <summary>
    ///     Combines this specification with another specification using the logical AND operator.
    /// </summary>
    /// <param name="specification">
    ///     The specification to combine with this specification.
    /// </param>
    /// <returns>
    ///     A new specification that represents the logical AND of this specification and the other specification.
    /// </returns>
    public SpecificationBase<TModel, TMetadata> And(SpecificationBase<TModel, TMetadata> specification) =>
        new AndSpecification<TModel, TMetadata>(this, specification);

    /// <summary>
    ///     Combines this specification with another specification using the logical OR operator.
    /// </summary>
    /// <param name="specification">
    ///     The specification to combine with this specification.
    /// </param>
    /// <returns>
    ///     A new specification that represents the logical OR of this specification and the other specification.
    /// </returns>
    public SpecificationBase<TModel, TMetadata> Or(SpecificationBase<TModel, TMetadata> specification) =>
        new OrSpecification<TModel, TMetadata>(this, specification);

    /// <summary>
    ///     Combines this specification with another specification using the logical XOR operator.
    /// </summary>
    /// <param name="specification">
    ///     The specification to combine with this specification.
    /// </param>
    /// <returns>
    ///     A new specification that represents the logical XOR of this specification and the other specification.
    /// </returns>
    public SpecificationBase<TModel, TMetadata> XOr(SpecificationBase<TModel, TMetadata> specification) =>
        new XOrSpecification<TModel, TMetadata>(this, specification);

    /// <summary>
    ///     Negates this specification.
    /// </summary>
    /// <returns>
    ///     A new specification that represents the logical NOT of this specification.
    /// </returns>
    public SpecificationBase<TModel, TMetadata> Not() =>
        new NotSpecification<TModel, TMetadata>(this);

    /// <summary>
    ///     Changes the <typeparamref name="TModel" /> <see cref="Type" /> of the specification.
    /// </summary>
    /// <param name="childModelSelector">
    ///     A function that takes the model and returns the child model to evaluate the specification against.
    /// </param>
    /// <typeparam name="TNewModel"></typeparam>
    /// <returns>
    ///     A new specification that represents the same specification but with a different <typeparamref name="TModel" />.
    /// </returns>
    public SpecificationBase<TNewModel, TMetadata> ChangeModel<TNewModel>(
        Func<TNewModel, TModel> childModelSelector) =>
        new ChangeModelTypeSpecification<TNewModel, TModel, TMetadata>(this, childModelSelector);

    /// <summary>
    ///     Changes the <typeparamref name="TModel" /> <see cref="Type" /> of the specification.
    /// </summary>
    /// <typeparam name="TDerivedModel">
    ///     The type to change the <typeparamref name="TModel" /> to. This type must be a subclass of
    ///     <typeparamref name="TModel" />.
    /// </typeparam>
    /// <returns>
    ///     A new specification that represents the same specification but with a different <typeparamref name="TModel" />.
    /// </returns>
    public SpecificationBase<TDerivedModel, TMetadata> ChangeModel<TDerivedModel>()
        where TDerivedModel : TModel =>
        new ChangeModelTypeSpecification<TDerivedModel, TModel, TMetadata>(this, model => model);

    /// <summary>
    ///     Serializes the logical hierarchy of the specification to a string.
    /// </summary>
    /// <returns>
    ///     A string that represents the logical hierarchy of the specification.
    /// </returns>
    public override string ToString() => Description;

    /// <summary>
    ///     Combines two specifications using the logical AND operator.
    /// </summary>
    /// <param name="left">
    ///     The left operand of the AND operation.
    /// </param>
    /// <param name="right">
    ///     The right operand of the AND operation.
    /// </param>
    /// <returns>
    ///     A new specification that represents the logical AND of the two specifications.
    /// </returns>
    public static SpecificationBase<TModel, TMetadata> operator &(
        SpecificationBase<TModel, TMetadata> left,
        SpecificationBase<TModel, TMetadata> right) =>
        left.And(right);

    /// <summary>
    ///     Combines two specifications using the logical OR operator.
    /// </summary>
    /// <param name="left">
    ///     The left operand of the OR operation.
    /// </param>
    /// <param name="right">
    ///     The right operand of the OR operation.
    /// </param>
    /// <returns>
    ///     A new specification that represents the logical OR of the two specifications.
    /// </returns>
    public static SpecificationBase<TModel, TMetadata> operator |(
        SpecificationBase<TModel, TMetadata> left,
        SpecificationBase<TModel, TMetadata> right) =>
        left.Or(right);

    /// <summary>
    ///     Combines two specifications using the logical XOR operator.
    /// </summary>
    /// <param name="left">
    ///     The left operand of the XOR operation.
    /// </param>
    /// <param name="right">
    ///     The right operand of the XOR operation.
    /// </param>
    /// <returns>
    ///     A new specification that represents the logical XOR of the two specifications.
    /// </returns>
    public static SpecificationBase<TModel, TMetadata> operator ^(
        SpecificationBase<TModel, TMetadata> left,
        SpecificationBase<TModel, TMetadata> right) =>
        left.XOr(right);

    /// <summary>
    ///     Negates a specification.
    /// </summary>
    /// <param name="specification">
    ///     The specification to negate.
    /// </param>
    /// <returns>
    ///     A new specification that represents the logical NOT of the specification.
    /// </returns>
    public static SpecificationBase<TModel, TMetadata> operator !(
        SpecificationBase<TModel, TMetadata> specification) =>
        specification.Not();
}