using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Karlssberg.Motive;

[Serializable]
public class SpecificationException : Exception
{
    public SpecificationException()
    {
    }

    protected SpecificationException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }

    public SpecificationException(string message)
        : base(message)
    {
    }
    
    public SpecificationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}