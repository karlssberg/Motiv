using AutoFixture;
using AutoFixture.AutoNSubstitute;
using Motiv.HigherOrderProposition;
using Motiv.Tests.Customizations;
using Motiv.Tests.HigherOrderProposition;

namespace Motiv.Tests;

/// <inheritdoc cref="AutoFixture.Xunit2.AutoDataAttribute"/>
internal class AutoDataAttribute()
    : AutoFixture.Xunit2.AutoDataAttribute(FixtureFactory)
{
    private static IFixture FixtureFactory()
    {
        var fixture = new Fixture()
            .Customize(new AutoNSubstituteCustomization { ConfigureMembers = true, GenerateDelegates = true })
            .Customize(new BooleanResultBaseCustomization())
            .Customize(new MetadataNodeCustomization())
            .Customize(new PolicyResultBaseCustomization())
            .Customize(new ResultDescriptionBaseCustomization())
            .Customize(new ExplanationCustomization());

        fixture.Behaviors.OfType<ThrowingRecursionBehavior>().ToList()
            .ForEach(b => fixture.Behaviors.Remove(b));
        fixture.Behaviors.Add(new OmitOnRecursionBehavior());

        return fixture;
    }
}


