namespace Karlssberg.Motive;

public static class SpecificationBaseExtensions
{
    public static Func<TModel, bool> ToPredicate<TModel, TMetadata>(this SpecificationBase<TModel, TMetadata> specification) =>
        specification.IsSatisfiedBy;
}