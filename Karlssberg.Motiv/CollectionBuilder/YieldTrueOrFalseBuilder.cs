namespace Karlssberg.Motiv.CollectionBuilder;

public class YieldTrueOrFalseBuilder<TModel, TMetadata>(
    IYieldMetadata<TModel, TMetadata> builder, 
    Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenTrue)
{
    public IYieldFalseMetadata<TModel, TMetadata> YieldWhenAnyFalse(
        Func<BooleanResultWithModel<TModel, TMetadata>, TMetadata> whenFalse)
    {
        return builder.YieldWhenAny(GetResults);

        IEnumerable<TMetadata> GetResults(bool isSatisfied, IEnumerable<BooleanResultWithModel<TModel, TMetadata>> results)
        {
            var resultList = results.ToList();
            return isSatisfied switch
            {
                true => GetTrueResults(),
                false => GetFalseResults()
            };

            IEnumerable<TMetadata> GetTrueResults() => resultList.
                Where(result => result.IsSatisfied == isSatisfied)
                .Select(whenTrue);

            IEnumerable<TMetadata> GetFalseResults() => resultList
                .Where(result => result.IsSatisfied != isSatisfied)
                .Select(whenFalse);
        }
    }
}