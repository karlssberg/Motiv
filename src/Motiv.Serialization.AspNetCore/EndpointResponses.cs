using System.Text.Json;
using Microsoft.AspNetCore.Http;

namespace Motiv.Serialization.AspNetCore;

/// <summary>
/// The pieces of response building the rule and proposition surfaces genuinely share: a malformed
/// request is refused in the same words whichever surface received it, and a stored document is
/// handed back the same way. Restating either in both files invites them to drift apart, which the
/// caller would see as two endpoints disagreeing about the same mistake.
/// </summary>
internal static class EndpointResponses
{
    internal static IResult MissingDocument(JsonSerializerOptions json) =>
        Results.Json(new ErrorResponse("The request must include a document."), json, statusCode: 400);

    internal static IResult NonPositiveBaseVersion(JsonSerializerOptions json) =>
        Results.Json(
            new ErrorResponse("baseVersion must be a positive integer; versions start at 1."),
            json, statusCode: 400);

    /// <summary>
    /// A stored document as a JSON element, or null when there is no document. The element is cloned
    /// out of the parsed document, which owns the buffer backing it and is disposed here.
    /// </summary>
    internal static JsonElement? DocumentElement(string? documentJson)
    {
        if (documentJson is null)
            return null;

        using var parsed = JsonDocument.Parse(documentJson);
        return parsed.RootElement.Clone();
    }
}
