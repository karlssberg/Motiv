namespace Motiv.Serialization.AspNetCore.Tests;

public class MotivRulesOptionsTests
{
    [Fact]
    public void Should_resolve_a_registered_model_id_and_fall_back_to_the_type_name()
    {
        // Arrange
        var options = new MotivRulesOptions().AddModel<int>("number");

        // Act & Assert
        options.ResolveModelId(typeof(int)).ShouldBe("number");
        options.ResolveModelId(typeof(string)).ShouldBe("String");
    }

    [Fact]
    public void Should_reject_a_duplicate_model_id()
    {
        // Arrange
        var options = new MotivRulesOptions().AddModel<int>("number");

        // Act & Assert
        Should.Throw<ArgumentException>(() => options.AddModel<long>("number"));
    }
}
