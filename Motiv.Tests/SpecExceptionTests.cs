namespace Motiv.Tests;

public class SpecExceptionTests
{
    [Fact]
    public void Constructor_NoParameters_CreatesInstance()
    {
        var exception = new SpecException();

        exception.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithMessage_SetsMessage()
    {
        var message = "Test message";

        var exception = new SpecException(message);

        message.Should().Be(exception.Message);
    }

    [Fact]
    public void Constructor_WithMessageAndInnerException_SetsMessageAndInnerException()
    {
        var message = "Test message";
        var innerException = new Exception("Inner exception");

        var exception = new SpecException(message, innerException);

        message.Should().Be(exception.Message);
        innerException.Should().Be(exception.InnerException);
    }
}
