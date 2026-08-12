namespace Motiv.Serialization;

/// <summary>Derives a <see cref="ChangeClassification"/> from a proposed document diff.</summary>
internal static class ChangeClassifier
{
    /// <summary>Pure function of the proposed and base documents — storing derivable facts invites
    /// drift. Rollback intent is the exception the diff cannot recover.</summary>
    /// <param name="proposedDocumentJson">The proposed new document, or null to delete.</param>
    /// <param name="baseDocumentJson">The document being replaced, or null when the target is new.</param>
    /// <param name="targetExists">Whether the change's target already exists.</param>
    /// <param name="specIsAsync">Reports whether a referenced spec name is evaluated asynchronously.</param>
    /// <param name="rollbackOfVersion">The version this change rolls back to, if any.</param>
    public static ChangeClassification Classify(
        string? proposedDocumentJson,
        string? baseDocumentJson,
        bool targetExists,
        Func<string, bool> specIsAsync,
        int? rollbackOfVersion)
    {
        var proposedDocument = TryParse(proposedDocumentJson);
        var baseDocument = TryParse(baseDocumentJson);

        return new ChangeClassification(
            IsCreation: !targetExists,
            IsDeletion: targetExists && proposedDocumentJson is null,
            IsMetadataOnly: IsMetadataOnlyChange(proposedDocument, baseDocument),
            TouchesAsyncSpec: TouchesAsyncSpec(proposedDocument, specIsAsync),
            IsRollback: rollbackOfVersion.HasValue,
            RollbackOfVersion: rollbackOfVersion);
    }

    private static bool IsMetadataOnlyChange(RuleDocument? proposedDocument, RuleDocument? baseDocument) =>
        proposedDocument is not null
        && baseDocument is not null
        && RuleDocumentComparer.StructurallyEqual(proposedDocument, baseDocument);

    private static bool TouchesAsyncSpec(RuleDocument? proposedDocument, Func<string, bool> specIsAsync) =>
        proposedDocument is not null
        && DocumentReferences.From(proposedDocument).Any(specIsAsync);

    // Unparseable documents classify as all-false — validation elsewhere rejects them before publish.
    private static RuleDocument? TryParse(string? json)
    {
        if (json is null)
            return null;

        var errors = new List<RuleError>();
        var document = new RuleDocumentParser(new RuleSerializerOptions()).Parse(json, errors);
        return errors.Count == 0 ? document : null;
    }
}
