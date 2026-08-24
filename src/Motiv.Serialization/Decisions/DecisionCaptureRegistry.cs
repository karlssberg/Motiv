namespace Motiv.Serialization;

/// <summary>
/// How much of an evaluated model the decision log may keep, chosen per model type.
/// </summary>
/// <remarks>
/// <para>
/// The product cannot choose this: it depends on the adopter's data and their regime. So it is a seam,
/// and it is a seam with <strong>no default</strong> — a rule marked <c>audited</c> over a model type
/// with nothing registered here will not bind. A whole-model default that is on by omission is the
/// default-credentials trap wearing a compliance badge.
/// </para>
/// <para>
/// The posture chosen is the <strong>replay ceiling</strong>. <see cref="ReferenceOnly{TModel}"/> is
/// the recommended production posture because it lets erasure and audit coexist: erase the subject in
/// the adopter's system of record, and the decision record survives without personal data while replay
/// correctly becomes impossible.
/// </para>
/// </remarks>
public sealed class DecisionCaptureRegistry
{
    private readonly Dictionary<Type, Func<object, DecisionInput>> _postures = [];

    /// <summary>
    /// Stores the model as evaluated. <strong>Development only</strong> — this keeps whatever the model
    /// holds, including whatever of it is personal.
    /// </summary>
    /// <typeparam name="TModel">The model type this posture applies to.</typeparam>
    public DecisionCaptureRegistry StoreWhole<TModel>() =>
        Register<TModel>(model => DecisionInput.Whole(model));

    /// <summary>Stores an adopter-supplied projection of the model. Replay goes as far as the mask left it.</summary>
    /// <typeparam name="TModel">The model type this posture applies to.</typeparam>
    /// <param name="projection">The masked view to store.</param>
    /// <exception cref="ArgumentNullException"><paramref name="projection"/> is null.</exception>
    public DecisionCaptureRegistry Redact<TModel>(Func<TModel, object?> projection)
    {
        if (projection is null) throw new ArgumentNullException(nameof(projection));
        return Register<TModel>(model => DecisionInput.Redacted(projection(model)));
    }

    /// <summary>
    /// Stores only a key naming the model in the adopter's own system of record. The GDPR-clean
    /// posture, and the recommended one for production.
    /// </summary>
    /// <typeparam name="TModel">The model type this posture applies to.</typeparam>
    /// <param name="keySelector">Reads the key identifying this model elsewhere.</param>
    /// <exception cref="ArgumentNullException"><paramref name="keySelector"/> is null.</exception>
    public DecisionCaptureRegistry ReferenceOnly<TModel>(Func<TModel, string> keySelector)
    {
        if (keySelector is null) throw new ArgumentNullException(nameof(keySelector));
        return Register<TModel>(model => DecisionInput.Reference(keySelector(model)));
    }

    /// <summary>Whether a posture has been chosen for <paramref name="modelType"/>.</summary>
    internal bool Covers(Type modelType) => _postures.ContainsKey(modelType);

    /// <summary>Captures a model, or returns null when no posture covers its type.</summary>
    internal DecisionInput? Capture<TModel>(TModel model) =>
        model is not null && _postures.TryGetValue(typeof(TModel), out var posture)
            ? posture(model)
            : null;

    private DecisionCaptureRegistry Register<TModel>(Func<TModel, DecisionInput> posture)
    {
        _postures[typeof(TModel)] = model => posture((TModel)model);
        return this;
    }
}
