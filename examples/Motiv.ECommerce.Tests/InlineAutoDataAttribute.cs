namespace Motiv.ECommerce.Tests;

internal class InlineAutoDataAttribute(params object?[] values)
    : AutoFixture.Xunit2.InlineAutoDataAttribute(new AutoDataAttribute(), values);