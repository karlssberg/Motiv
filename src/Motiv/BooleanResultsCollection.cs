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
    private readonly IEnumerable<BooleanResult<TModel, TMetadata>> _resultsSource;

    private BooleanResult<TModel, TMetadata>[]? _results;
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
        _resultsSource = results.ThrowIfNull(nameof(results));
    }

    // Each projection below streams a single evaluation pass and retains only the projected data, so
    // the full result trees stay collectible.  Results is the exception: reading it is an explicit
    // request for the result objects, so they are materialized and later projections reuse them.
    private IEnumerable<BooleanResult<TModel, TMetadata>> Source => _results ?? _resultsSource;

    /// <summary>
    /// The raw boolean results.
    /// </summary>
    public IEnumerable<BooleanResultBase<TMetadata>> Results =>
        _results ??= _resultsSource as BooleanResult<TModel, TMetadata>[] ?? _resultsSource.ToArray();

    /// <summary>
    /// The models that were evaluated.
    /// </summary>
    public IEnumerable<TModel> Models =>
        _models ??= Source.Select(r => r.Model).ToArray();

    /// <summary>
    /// The collection of metadata values from the boolean results.
    /// </summary>
    public IEnumerable<TMetadata> Values =>
        _values ??= Source.SelectMany(b => b.Values).ToArray();

    /// <summary>
    /// The aggregated boolean outcome.
    /// </summary>
    public IEnumerable<string> Assertions =>
        _assertions ??= Source
            .SelectMany(b => b.Assertions)
            .Distinct()
            .ToArray();


    /// <inheritdoc cref="IEnumerable{T}.GetEnumerator" />
    public IEnumerator<TModel> GetEnumerator()
    {
        var models = _satisfiedModels ??= Source.Where(r => r.Satisfied).Select(r => r.Model).ToArray();
        return ((IEnumerable<TModel>)models).GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
