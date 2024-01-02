using AutoFixture;
using AutoFixture.AutoNSubstitute;
using AutoFixture.Xunit2;

namespace Karlssberg.Motiv.Poker.Tests;

internal class AutoParamsAttribute(params object?[] values) : InlineAutoDataAttribute(new CustomizedAutoDataAttribute(), values)
{
    private class CustomizedAutoDataAttribute() : AutoDataAttribute(CreateFixture)
    {
        private static IFixture CreateFixture()
        {
            return new Fixture()
                .Customize(new AutoNSubstituteCustomization { ConfigureMembers = true, GenerateDelegates = true });
        }
    }
}
