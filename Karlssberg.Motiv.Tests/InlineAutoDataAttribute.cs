using AutoFixture;
using AutoFixture.AutoNSubstitute;
using AutoFixture.Xunit2;

namespace Karlssberg.Motiv.Tests;

internal class InlineAutoDataAttribute(params object?[] values) 
    : AutoFixture.Xunit2.InlineAutoDataAttribute(new CustomizedAutoDataAttribute(), values)
{
    private class CustomizedAutoDataAttribute() 
        : AutoDataAttribute(CreateFixture)
    {
        private static IFixture CreateFixture() =>
            new Fixture()
                .Customize(new AutoNSubstituteCustomization { ConfigureMembers = true, GenerateDelegates = true });
    }
}