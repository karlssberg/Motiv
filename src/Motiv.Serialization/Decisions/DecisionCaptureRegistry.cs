using Motiv.Diagnostics;

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
    private ExplanationDetail _ceiling = ExplanationDetail.Full;

    /// <summary>
    /// The most explanation text a span may carry, given what the postures registered here say about
    /// the adopter's data. Applied to <see cref="MotivTelemetry.ExplanationDetail"/> when a
    /// <see cref="DecisionLog"/> is created, so PII posture is stated once rather than twice.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>motiv.reason</c> and <c>motiv.assertions</c> carry text authored by the proposition, which
    /// can interpolate the model — <c>model =&gt; $"income is {model.Income}"</c>. An adopter who
    /// registered <see cref="Redact{TModel}"/> or <see cref="ReferenceOnly{TModel}"/> has said the
    /// model may not be stored raw, and neither their projection nor their key selector is applied to
    /// assertion text: the same values would reach the trace exporter untouched. So those two yield
    /// <see cref="ExplanationDetail.None"/>. <see cref="StoreWhole{TModel}"/> yields
    /// <see cref="ExplanationDetail.Full"/> — it already accepts raw model data in durable storage,
    /// and trace text is strictly less exposure than that. An empty registry has made no statement
    /// and yields <see cref="ExplanationDetail.Full"/>, leaving the existing default alone.
    /// </para>
    /// <para>
    /// <strong><see cref="ExplanationDetail.ReasonOnly"/> is never derived, and that is deliberate.</strong>
    /// It looks like the middle of three privacy settings and is not one:
    /// <see cref="BooleanResultBase.Reason"/> is built from the same authored strings as
    /// <see cref="BooleanResultBase.Assertions"/>, so dropping the array reduces volume and cost
    /// without reducing exposure. It remains available to an adopter who sets it by hand for those
    /// reasons; what this must not do is offer it as a privacy compromise, because there is no data
    /// it protects that <see cref="ExplanationDetail.Full"/> exposes.
    /// </para>
    /// <para>
    /// Several model types can be registered with different postures, so this is the strictest of
    /// them: the setting it feeds is process-wide, and a ceiling that satisfied only the most
    /// permissive registration would leak the strictest one's data.
    /// </para>
    /// </remarks>
    public ExplanationDetail ExplanationCeiling => _ceiling;

    /// <summary>
    /// Stores the model as evaluated. <strong>Development only</strong> — this keeps whatever the model
    /// holds, including whatever of it is personal.
    /// </summary>
    /// <typeparam name="TModel">The model type this posture applies to.</typeparam>
    public DecisionCaptureRegistry StoreWhole<TModel>() =>
        Register<TModel>(model => DecisionInput.Whole(model), ExplanationDetail.Full);

    /// <summary>Stores an adopter-supplied projection of the model. Replay goes as far as the mask left it.</summary>
    /// <typeparam name="TModel">The model type this posture applies to.</typeparam>
    /// <param name="projection">The masked view to store.</param>
    /// <exception cref="ArgumentNullException"><paramref name="projection"/> is null.</exception>
    public DecisionCaptureRegistry Redact<TModel>(Func<TModel, object?> projection)
    {
        if (projection is null) throw new ArgumentNullException(nameof(projection));
        return Register<TModel>(model => DecisionInput.Redacted(projection(model)), ExplanationDetail.None);
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
        return Register<TModel>(model => DecisionInput.Reference(keySelector(model)), ExplanationDetail.None);
    }

    /// <summary>Whether a posture has been chosen for <paramref name="modelType"/>.</summary>
    internal bool Covers(Type modelType) => _postures.ContainsKey(modelType);

    /// <summary>Captures a model, or returns null when no posture covers its type.</summary>
    internal DecisionInput? Capture<TModel>(TModel model) =>
        model is not null && _postures.TryGetValue(typeof(TModel), out var posture)
            ? posture(model)
            : null;

    private DecisionCaptureRegistry Register<TModel>(
        Func<TModel, DecisionInput> posture, ExplanationDetail ceiling)
    {
        _postures[typeof(TModel)] = model => posture((TModel)model);

        // The enum is ordered by strictness — Full, ReasonOnly, None — so the strictest is the
        // greatest. Re-registering a model type does not relax the ceiling: another type's stricter
        // posture is still in force, and this cannot see whose registration it replaced.
        if (ceiling > _ceiling)
            _ceiling = ceiling;

        return this;
    }
}
