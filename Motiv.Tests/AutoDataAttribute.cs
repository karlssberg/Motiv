using AutoFixture;
using AutoFixture.AutoNSubstitute;

namespace Motiv.Tests;

/// <inheritdoc cref="AutoFixture.Xunit2.AutoDataAttribute"/>
internal class AutoDataAttribute() 
    : AutoFixture.Xunit2.AutoDataAttribute(() =>
        new Fixture()
        .Customize(
            new AutoNSubstituteCustomization
            {
                ConfigureMembers = true,
                GenerateDelegates = true
            }));