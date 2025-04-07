using System.Runtime.Serialization;

namespace Motiv.Generator.FluentFactory;

public class FluentException : Exception
{
    public FluentException()
    {
    }

    protected FluentException(SerializationInfo info, StreamingContext context) : base(info, context)
    {
    }

    public FluentException(string message) : base(message)
    {
    }

    public FluentException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
