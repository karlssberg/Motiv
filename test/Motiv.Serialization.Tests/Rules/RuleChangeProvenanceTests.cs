using Motiv.Serialization;

namespace Motiv.Serialization.Tests.Rules;

public class RuleChangeProvenanceTests
{
    [Fact]
    public void Should_default_the_build_id_to_the_current_build()
    {
        // Act
        var provenance = new RuleChangeProvenance("alice").WithDefaults();

        // Assert — a code-defined rule tracks code with no version bump, so the row must pin the build
        provenance.BuildId!.ShouldBe(BuildIdentity.Current);
        provenance.BuildId.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Should_keep_an_explicit_build_id()
    {
        // Act
        var provenance = new RuleChangeProvenance("alice", BuildId: "deadbeef").WithDefaults();

        // Assert
        provenance.BuildId!.ShouldBe("deadbeef");
    }

    [Fact]
    public void Should_name_the_system_as_author_when_no_principal_is_involved()
    {
        // Assert — a rebind or a startup load is not a person's edit
        RuleChangeProvenance.System.Author.ShouldBe("system");
    }
}
