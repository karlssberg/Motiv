using System.Runtime.InteropServices;
using System.Runtime.Serialization;

namespace Karlssberg.Motiv;

[Serializable]
public class SpecException : Exception
{
    public SpecException()
    {
    }

    protected SpecException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }

    public SpecException(string message)
        : base(message)
    {
    }
    
    public SpecException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}