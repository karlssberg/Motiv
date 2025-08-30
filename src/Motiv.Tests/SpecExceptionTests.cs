namespace Motiv.Tests;

public class SpecExceptionTests
{
    [Fact]
    public void Constructor_NoParameters_CreatesInstance()
    {
        var exception = new SpecException();

        exception.ShouldNotBeNull();
    }

    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        var message = "Test message";

        var exception = new SpecException(message);

        message.ShouldBe(exception.Message);
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_SetsMessageAndInnerException()
    {
        var message = "Test message";
        var innerException = new Exception("Inner exception");

        var exception = new SpecException(message, innerException);

        message.ShouldBe(exception.Message);
        innerException.ShouldBe(exception.InnerException);
    }
}
