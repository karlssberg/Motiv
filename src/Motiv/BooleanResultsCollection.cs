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
public class BooleanResultsCollection<TModel, TMetadata>
    : IEnumerable<TModel>
{
    private readonly BooleanResult<TModel, TMetadata>[] _results;
    private readonly BooleanResult<TModel, TMetadata>[] _satisfied;

    private TModel[]? _models;
    private TMetadata[]? _values;
    private string[]? _assertions;
    private TModel[]? _satisfiedModels;

    /// <summary>
    /// Initializes a new instance of the <see cref="BooleanResultsCollection{TModel, TMetadata}"/> class.
    /// </summary>
    /// <param name="results">The boolean results for the models.</param>
    public BooleanResultsCollection(IEnumerable<BooleanResult<TModel, TMetadata>> results)
    {
        _results = results as BooleanResult<TModel, TMetadata>[] ?? results.ToArray();
        _satisfied = _results.Where(r => r.Satisfied).ToArray();
    }

    /// <summary>
    /// The raw boolean results.
    /// </summary>
    public IEnumerable<BooleanResultBase<TMetadata>> Results => _results;

    /// <summary>
    /// The models that were evaluated.
    /// </summary>
    public IEnumerable<TModel> Models =>
        _models ??= _results.Select(r => r.Model).ToArray();

    /// <summary>
    /// The collection of metadata values from the boolean results.
    /// </summary>
    public IEnumerable<TMetadata> Values =>
        _values ??= _results.SelectMany(b => b.Values).ToArray();

    /// <summary>
    /// The aggregated boolean outcome.
    /// </summary>
    public IEnumerable<string> Assertions =>
        _assertions ??= _results
            .SelectMany(b => b.Assertions)
            .Distinct()
            .ToArray();


    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator" />
    public IEnumerator<TModel> GetEnumerator()
    {
        var models = _satisfiedModels ??= _satisfied.Select(r => r.Model).ToArray();
        return ((IEnumerable<TModel>)models).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
