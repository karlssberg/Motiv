using System.Linq;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Shouldly;
using Xunit;

namespace Motiv.Testing;

/// <summary>
/// That no test assembly keeping throwaway SQLite files on disk reaches for the process-global
/// connection-pool clear.
/// </summary>
/// <remarks>
/// <para>
/// <c>SqliteConnection.ClearAllPools()</c> is process-global, and xunit runs test classes in parallel
/// while each fixture owns a private GUID-named database file. One class's teardown therefore disposes
/// the <c>SQLitePCL.sqlite3</c> handle underneath a connection another class has already leased from
/// the pool and is about to open, and the victim throws <see cref="ObjectDisposedException" /> out of
/// <c>SqliteConnection.Open()</c> — a window nanoseconds wide, reachable only under load
/// (<see href="https://github.com/karlssberg/Motiv/issues/219">#219</see>). A fixture releases its own
/// handles by never taking them: <c>Pooling=False</c> in the connection string, which is what
/// <c>SqliteDecisionFixture</c> had been doing, alone, all along.
/// </para>
/// <para>
/// The fix is the <b>absence</b> of a call, and no behavioural test keeps an absence absent — so this
/// gate is the durable half. It is read from the assembly's <b>metadata</b> rather than its source, so
/// a comment cannot satisfy it, following <c>HigherOrderSeamGateTests</c>. A reference emitted by code
/// that is never reached still leaves a <see cref="MemberReference" /> row, which is the point: the
/// gate asks whether the assembly mentions the name at all, not whether the call executes.
/// </para>
/// <para>
/// Compiled into each test assembly that keeps SQLite files on disk, via <c>Compile Include</c>, the
/// way <c>StoreConformance</c> already is. The claim is necessarily per-assembly — metadata is only
/// readable one assembly at a time — and sharing the source is what makes the three copies provably
/// the same gate rather than three that have drifted.
/// </para>
/// </remarks>
public class PoolClearGateTests
{
    [Fact]
    public void Should_not_reference_the_process_global_pool_clear_anywhere_in_this_assembly()
    {
        // Arrange — this assembly, whichever one the file was compiled into
        using var stream = File.OpenRead(typeof(PoolClearGateTests).Assembly.Location);
        using var reader = new PEReader(stream);
        var metadata = reader.GetMetadataReader();

        // Act — filtered on the member name alone. Narrowing the predicate by declaring type would
        // turn an unrecognised shape into a false green, which is the one failure a gate may not have.
        var offenders = metadata.MemberReferences
            .Select(metadata.GetMemberReference)
            .Where(member => metadata.GetString(member.Name) == "ClearAllPools")
            .Select(member => $"{DeclaringName(metadata, member.Parent)}.ClearAllPools")
            .ToList();

        // Assert
        offenders.ShouldBeEmpty(
            "a pooled connection holds the database file open, and clearing the pool to release it is "
            + "process-global: use `Pooling=False` in the connection string instead (#219)");
    }

    /// <summary>
    /// The declaring name of a member reference, for the failure message. Total over
    /// <see cref="EntityHandle" />'s kinds on purpose: a <c>MemberRef</c> parent may be a
    /// <c>TypeRef</c>, <c>TypeDef</c>, <c>TypeSpec</c>, <c>ModuleRef</c> or <c>MethodDef</c>
    /// (ECMA-335 §22.25), and a cast that assumes one of them would replace this gate's designed
    /// message with an <see cref="InvalidCastException" /> — on the failure path, the only path that
    /// ever runs it.
    /// </summary>
    private static string DeclaringName(MetadataReader metadata, EntityHandle parent) =>
        parent.Kind switch
        {
            HandleKind.TypeReference => Qualified(metadata, metadata.GetTypeReference((TypeReferenceHandle)parent)),
            HandleKind.TypeDefinition => metadata.GetString(
                metadata.GetTypeDefinition((TypeDefinitionHandle)parent).Name),
            _ => $"<{parent.Kind}>"
        };

    private static string Qualified(MetadataReader metadata, TypeReference type)
    {
        var name = metadata.GetString(type.Name);
        return type.Namespace.IsNil ? name : $"{metadata.GetString(type.Namespace)}.{name}";
    }
}
