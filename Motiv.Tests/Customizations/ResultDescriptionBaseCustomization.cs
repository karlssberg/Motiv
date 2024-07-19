using AutoFixture;

namespace Motiv.Tests.Customizations;


public class ResultDescription(string statement, string reason, IEnumerable<string> justification) : ResultDescriptionBase
{
    internal override string Statement => statement;
    public override string Reason => reason;
    internal override int CausalOperandCount => 1;
    public override IEnumerable<string> GetJustificationAsLines() => justification;
}

public class ResultDescriptionBaseCustomization : ICustomization
{
    public void Customize(IFixture fixture)
    {
        fixture.Customize<ResultDescriptionBase>(c => c
            .FromFactory((string statement, string reason, IEnumerable<string> justification) =>
                new ResultDescription(statement, reason, justification)));
    }
}
