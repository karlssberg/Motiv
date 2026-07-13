namespace Motiv.SyncToAsyncAdapter;

/// <summary>
/// Marks an asynchronous specification that adapts a synchronous specification. Descriptions and
/// justification traversal unwrap adapters so that lifted specifications render identically to their
/// synchronous counterparts.
/// </summary>
internal interface ISyncSpecAdapter
{
    /// <summary>The synchronous specification being adapted.</summary>
    SpecBase UnderlyingSpec { get; }
}
