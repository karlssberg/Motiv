using Karlssberg.Motive.And;
using Karlssberg.Motive.Not;
using Karlssberg.Motive.Or;
using Karlssberg.Motive.XOr;

namespace Karlssberg.Motive;

public abstract class SpecificationBase<TModel, TMetadata>
{
    public abstract string Description { get; }
    
    public abstract BooleanResultBase<TMetadata> Evaluate(TModel model);

    public SpecificationBase<TModel, TMetadata> And(SpecificationBase<TModel, TMetadata> specification) =>
        new AndSpecification<TModel, TMetadata>(this, specification);

    public SpecificationBase<TModel, TMetadata> Or(SpecificationBase<TModel, TMetadata> specification) =>
        new OrSpecification<TModel, TMetadata>(this, specification);

    public SpecificationBase<TModel, TMetadata> XOr(SpecificationBase<TModel, TMetadata> specification) =>
        new XOrSpecification<TModel, TMetadata>(this, specification);

    public SpecificationBase<TModel, TMetadata> Not() =>
        new NotSpecification<TModel, TMetadata>(this);
    public SpecificationBase<TNewModel, TMetadata> ChangeModel<TNewModel>(
        Func<TNewModel, TModel> childModelSelector) =>
        new ChangeModelSpecification<TNewModel, TModel, TMetadata>(this, childModelSelector);

    public SpecificationBase<TDerivedModel, TMetadata> ChangeModel<TDerivedModel>() where TDerivedModel : TModel => 
        new ChangeModelSpecification<TDerivedModel, TModel, TMetadata>(this, model => model);

    public override string ToString() => Description;

    public static SpecificationBase<TModel, TMetadata> operator &(
        SpecificationBase<TModel, TMetadata> left,
        SpecificationBase<TModel, TMetadata> right) =>
        left.And(right);

    public static SpecificationBase<TModel, TMetadata> operator |(
        SpecificationBase<TModel, TMetadata> left,
        SpecificationBase<TModel, TMetadata> right) =>
        left.Or(right);

    public static SpecificationBase<TModel, TMetadata> operator ^(
        SpecificationBase<TModel, TMetadata> left,
        SpecificationBase<TModel, TMetadata> right) =>
        left.XOr(right);

    public static SpecificationBase<TModel, TMetadata> operator !(
        SpecificationBase<TModel, TMetadata> specification) =>
        specification.Not();
}