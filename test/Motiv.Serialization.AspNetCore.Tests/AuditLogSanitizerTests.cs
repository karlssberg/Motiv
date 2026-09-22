namespace Motiv.Serialization.AspNetCore.Tests;

public class AuditLogSanitizerTests
{
    [Theory]
    [InlineData("alice", "alice")]
    [InlineData("evil\r\nMOTIV-AUDIT forged line", "evilMOTIV-AUDIT forged line")]
    [InlineData("split\nname", "splitname")]
    [InlineData("carriage\rname", "carriagename")]
    public void Should_strip_line_breaks_from_caller_supplied_audit_values(string value, string expected)
    {
        // Act & Assert — a crafted subject or artefact name must not forge extra audit lines
        MotivGovernanceEndpoints.ForLog(value).ShouldBe(expected);
    }
}
