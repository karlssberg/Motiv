using System.Collections;
using Motiv.HigherOrderProposition;

namespace Motiv;

/// <summary>
/// A collection of boolean results for a set of models.
/// </summary>
/// <param name="results">
/// The boolean results for the models.
/// </param>
/// <typeparam name="TModel">
/// The type of the models.
/// </typeparam>
/// <typeparam name="TMetadata">
/// The metdata values type in the boolean results.
/// </typeparam>
public class BooleanResultsCollection<TModel, TMetadata>(
    IEnumerable<BooleanResult<TModel, TMetadata>> results)
    : IEnumerable<TModel>
{
    private readonly IEnumerable<BooleanResult<TModel, TMetadata>> _satisfied =
        results.Where(r => r.Satisfied);

    private IEnumerable<TModel>? _models;
    private IEnumerable<TMetadata>? _values;
    private IEnumerable<string>? _assertions;

    /// <summary>
    /// The raw boolean results.
    /// </summary>
    public IEnumerable<BooleanResultBase<TMetadata>> Results => results;

    /// <summary>
    /// The models that were evaluated.
    /// </summary>
    public IEnumerable<TModel> Models =>
        _models ??= results.Select(r => r.Model);

    /// <summary>
    /// The collection of metadata values from the boolean results.
    /// </summary>
    public IEnumerable<TMetadata> Values =>
        _values ??= results.SelectMany(b => b.Values);

    /// <summary>
    /// The aggregated boolean outcome.
    /// </summary>
    public IEnumerable<string> Assertions =>
        _assertions ??= results
            .SelectMany(b => b.Assertions)
            .Distinct();


    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator" />
    public IEnumerator<TModel> GetEnumerator()
    {
        return _satisfied.Select(r => r.Model).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
