using Motiv.Serialization;

/// <summary>
/// Seam: gate persistence. The active approval-gate document is one rule document, so the store is
/// one text file — the library validates and binds it, this only has to hold it.
/// </summary>
/// <remarks>
/// Absent file means "no gate", which is the permissive default and the only lockout-safe bootstrap.
/// Clearing the gate deletes the file rather than writing a marker, so "no gate configured" has
/// exactly one representation on disk and cannot be confused with a gate that failed to write.
/// </remarks>
/// <param name="path">Where the active gate document is kept.</param>
public sealed class JsonFileGateStore(string path) : IGateStore
{
    private readonly object _gate = new();

    /// <inheritdoc />
    /// <remarks>
    /// A blank file reads as no gate. <see cref="ApprovalGate"/> refuses to start on a stored
    /// document it cannot bind, and an empty string is not a document — treating it as one would
    /// turn a truncated write into a host that will not boot.
    /// </remarks>
    public string? Load()
    {
        lock (_gate)
        {
            if (!File.Exists(path))
                return null;

            var contents = File.ReadAllText(path);
            return string.IsNullOrWhiteSpace(contents) ? null : contents;
        }
    }

    /// <inheritdoc />
    public void Save(string? documentJson)
    {
        lock (_gate)
        {
            if (documentJson is null)
                File.Delete(path);
            else
                File.WriteAllText(path, documentJson);
        }
    }
}
