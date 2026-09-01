using Motiv.RuleAuthoring.Blazor.Authoring;
using Motiv.RuleAuthoring.Blazor.Domain;
using Shouldly;

namespace Motiv.RuleAuthoring.Blazor.Tests;

public class CustomerVocabularyTests
{
    private readonly AuthoringSession _session = new();

    public static TheoryData<string> RegisteredNames() => [.. CustomerVocabulary.Names];

    /// <remarks>
    /// The picker offers every registered name, so a name that does not bind is a proposition the
    /// author can choose and never author a valid document with.
    /// </remarks>
    [Theory]
    [MemberData(nameof(RegisteredNames))]
    public void Binds_and_evaluates_every_proposition_it_offers(string name)
    {
        var outcome = _session.Author(
            DraftNode.Spec(name),
            "vocabulary.probe",
            CustomerVocabulary.Samples[0]);

        outcome.Errors.ShouldBeEmpty();
        outcome.Satisfied.ShouldNotBeNull();
    }

    [Fact]
    public void Offers_the_names_it_registered()
    {
        CustomerVocabulary.Names.ShouldBe(
            CustomerVocabulary.Registry().Entries.Select(entry => entry.Name),
            ignoreOrder: true);
    }
}
