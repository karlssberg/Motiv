using Microsoft.AspNetCore.Http;

namespace Motiv.Serialization.AspNetCore;

/// <summary>
/// Pins one world for the duration of a request and stamps which one it was on the response.
/// </summary>
/// <remarks>
/// <para>
/// The pin is what makes a request coherent: a handler evaluating two rules would otherwise take two
/// independent reads and could see a combination that was never published. A request is the natural
/// unit — it is one decision from the caller's point of view.
/// </para>
/// <para>
/// The header is the same fact facing outward. A client that has seen <c>r7.p3</c> and then receives
/// <c>r5.p3</c> has been routed to a replica serving an older world. It cannot fix that on its own,
/// but it can know — which is the whole difference between eventual consistency and silent
/// divergence.
/// </para>
/// <para>
/// Only covers responses this filter actually wraps. A refusal issued by ASP.NET middleware ahead of
/// routing — <c>RequireAuthorization()</c>'s 401 for an unauthenticated caller, most notably — never
/// reaches this filter, so it carries no header; a caller with no credentials has no generation worth
/// comparing against anyway. See <see cref="MotivRulesEndpoints.GenerationHeader"/> for the rest of
/// the contract, including why a write's header intentionally understates.
/// </para>
/// </remarks>
internal sealed class MotivGenerationFilter(Func<DecisionSnapshot> pin) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        using var snapshot = pin();

        // Stamped through OnStarting so it lands on every response this filter wraps — including the
        // error paths this endpoint itself produces (an unknown rule, an invalid document), where
        // knowing which world refused you is worth as much as knowing which world served you. It does
        // NOT land on a refusal issued above this filter, e.g. RequireAuthorization()'s 401 — that
        // never reaches here at all.
        var response = context.HttpContext.Response;
        var token = snapshot.Generation.ToToken();
        response.OnStarting(static state =>
        {
            var carried = ((HttpResponse Response, string Token))state;
            carried.Response.Headers[MotivRulesEndpoints.GenerationHeader] = carried.Token;
            return Task.CompletedTask;
        }, (response, token));

        return await next(context).ConfigureAwait(false);
    }
}
