namespace Motiv.Diagnostics;

/// <summary>
/// Controls how much of an evaluation's explanation is attached to its telemetry span.
/// </summary>
/// <remarks>
/// The <c>motiv.reason</c> and <c>motiv.assertions</c> tags carry text authored by the proposition,
/// which can embed values from the model being evaluated — for example a <c>WhenTrue</c> delegate that
/// interpolates a customer's income. A deployment that cannot guarantee that text is free of sensitive
/// data can reduce or suppress it via <see cref="MotivTelemetry.ExplanationDetail" />. Reducing the
/// detail also avoids resolving the suppressed text, so its cost — and any exception from a user's
/// <c>WhenTrue</c>/<c>WhenFalse</c> delegate — is skipped.
/// </remarks>
public enum ExplanationDetail
{
    /// <summary>Tag the span with both <c>motiv.reason</c> and <c>motiv.assertions</c>. The default.</summary>
    Full,

    /// <summary>Tag the span with <c>motiv.reason</c> only; omit the <c>motiv.assertions</c> array.</summary>
    ReasonOnly,

    /// <summary>Tag neither — the span still carries the outcome, but no explanation text.</summary>
    None
}
