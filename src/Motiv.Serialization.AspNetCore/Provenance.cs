using Microsoft.AspNetCore.Http;

namespace Motiv.Serialization.AspNetCore;

/// <summary>Who a direct write is attributed to: the request's principal, and the note it carried.</summary>
internal static class Provenance
{
    public static RuleChangeProvenance Of(HttpContext http, string? changeNote = null) =>
        new(PrincipalIdentity.Subject(http.User), changeNote);
}
