using AutoFixture;

namespace Motiv.Tests.Customizations;

public class ExplanationCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Register((string reason) => new Explanation(reason, Enumerable.Empty<BooleanResultBase>()));
    }
}
