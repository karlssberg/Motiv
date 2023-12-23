namespace Karlssberg.Motive;

public static class SpecificationExtensions
{
    public static bool IsSatisfiedBy<TModel, TMetadata>(
        this SpecificationBase<TModel, TMetadata> specification,
        TModel model)
    {
        return specification.Evaluate(model).IsSatisfied;
    }
}

