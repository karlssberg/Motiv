namespace Motiv.Tests.MemoIdentity;

/// <summary>The memo-identity check: read each member twice and name those that came back as a new instance.</summary>
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
