using AutoFixture;
using AutoFixture.AutoNSubstitute;

namespace Karlssberg.Motiv.Tests;

/// <inheritdoc cref="AutoFixture.Xunit2.AutoDataAttribute"/>
internal class AutoDataAttribute() 
    : AutoFixture.Xunit2.AutoDataAttribute(CreateFixture)
{
    private static IFixture CreateFixture() =>
        new Fixture()
            .Customize(new AutoNSubstituteCustomization { ConfigureMembers = true, GenerateDelegates = true });
}