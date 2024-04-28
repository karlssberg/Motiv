namespace Karlssberg.Motiv.Tests;

/// <inheritdoc cref="AutoFixture.Xunit2.InlineAutoDataAttribute"/>
internal class InlineAutoDataAttribute(params object?[] values) 
    : AutoFixture.Xunit2.InlineAutoDataAttribute(new AutoDataAttribute(), values);