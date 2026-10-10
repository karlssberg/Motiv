namespace Motiv.Tests.MemoIdentity;

/// <summary>
/// The memo-identity check: read each member twice and name those that came back as a new instance. A
/// performance guard: a new instance is an equal value, built again (see <see cref="MemoisedResultCase" />).
/// </summary>
internal static class MemoProbe
{
    public static void ShouldRebuildNothingOnASecondRead<TSubject>(
        this TSubject subject,
        IEnumerable<(string Member, Func<TSubject, object> Read)> readers,
        string subjectName) =>
        readers
            .Where(reader => !ReferenceEquals(reader.Read(subject), reader.Read(subject)))
            .Select(reader => reader.Member)
            .ShouldBeEmpty($"'{subjectName}' rebuilt these on a second read");
}
