using AutoFixture;
using AutoFixture.AutoNSubstitute;

namespace Motiv.ECommerce.Tests;

internal class AutoDataAttribute() : AutoFixture.Xunit2.AutoDataAttribute(() =>
    new Fixture()
        .Customize(
            new AutoNSubstituteCustomization
            {
                ConfigureMembers = true,
                GenerateDelegates = true
            }));
