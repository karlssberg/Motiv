﻿using Karlssberg.Motiv.And;
using Karlssberg.Motiv.ChangeModelType;
using Karlssberg.Motiv.Not;
using Karlssberg.Motiv.Or;
using Karlssberg.Motiv.XOr;

namespace Karlssberg.Motiv;

/// <summary>
///     The base class for all specifications. A specification is an encapsulated predicate that can be evaluated
///     against a model.  When the predicate is evaluated, it returns a result that contains the Boolean result of the
///     predicate as well as metadata that captures the meaning behind the predicate.  By encapsulating the predicate we
///     can supply methods to assist with combining specifications together to form more complex specifications.
/// </summary>
/// <typeparam name="TModel">The model type that the specification will evaluate against</typeparam>
/// <typeparam name="TMetadata">The type of the metadata to associate with the predicate</typeparam>
public abstract class SpecBase<TModel, TMetadata>
{
    internal SpecBase()
    {
    }

    /// <summary>The description of the specification.  This is used for debugging/logging purposes.</summary>
    public abstract string Description { get; }

    /// <summary>
    ///     Evaluates the specification against the model and returns a result that contains the Boolean result of the
    ///     predicate in addition to the metadata.
    /// </summary>
    /// <param name="model"></param>
    /// <returns></returns>
    public abstract BooleanResultBase<TMetadata> IsSatisfiedBy(TModel model);

    /// <summary>Combines this specification with another specification using the logical AND operator.</summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical AND of this specification and the other specification.</returns>
    public SpecBase<TModel, TMetadata> And(SpecBase<TModel, TMetadata> spec) =>
        new AndSpec<TModel, TMetadata>(this, spec);

    /// <summary>Combines this specification with another specification using the logical OR operator.</summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical OR of this specification and the other specification.</returns>
    public SpecBase<TModel, TMetadata> Or(SpecBase<TModel, TMetadata> spec) =>
        new OrSpec<TModel, TMetadata>(this, spec);

    /// <summary>Combines this specification with another specification using the logical XOR operator.</summary>
    /// <param name="spec">The specification to combine with this specification.</param>
    /// <returns>A new specification that represents the logical XOR of this specification and the other specification.</returns>
    public SpecBase<TModel, TMetadata> XOr(SpecBase<TModel, TMetadata> spec) =>
        new XOrSpec<TModel, TMetadata>(this, spec);

    /// <summary>Negates this specification.</summary>
    /// <returns>A new specification that represents the logical NOT of this specification.</returns>
    public SpecBase<TModel, TMetadata> Not() =>
        new NotSpec<TModel, TMetadata>(this);

    /// <summary>Changes the <typeparamref name="TModel" /> <see cref="Type" /> of the specification.</summary>
    /// <param name="childModelSelector">
    ///     A function that takes the model and returns the child model to evaluate the
    ///     specification against.
    /// </param>
    /// <typeparam name="TNewModel"></typeparam>
    /// <returns>
    ///     A new specification that represents the same specification but with a different <typeparamref name="TModel" />
    ///     .
    /// </returns>
    public SpecBase<TNewModel, TMetadata> ChangeModel<TNewModel>(
        Func<TNewModel, TModel> childModelSelector) =>
        new ChangeModelTypeSpec<TNewModel, TModel, TMetadata>(this, childModelSelector);

    /// <summary>Changes the <typeparamref name="TModel" /> <see cref="Type" /> of the specification.</summary>
    /// <typeparam name="TDerivedModel">
    ///     The type to change the <typeparamref name="TModel" /> to. This type must be a subclass
    ///     of <typeparamref name="TModel" />.
    /// </typeparam>
    /// <returns>
    ///     A new specification that represents the same specification but with a different <typeparamref name="TModel" />
    ///     .
    /// </returns>
    public SpecBase<TDerivedModel, TMetadata> ChangeModel<TDerivedModel>()
        where TDerivedModel : TModel =>
        new ChangeModelTypeSpec<TDerivedModel, TModel, TMetadata>(this, model => model);

    /// <summary>Serializes the logical hierarchy of the specification to a string.</summary>
    /// <returns>A string that represents the logical hierarchy of the specification.</returns>
    public override string ToString() => Description;

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