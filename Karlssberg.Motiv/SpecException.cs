using System.Runtime.Serialization;

namespace Karlssberg.Motiv;

/// <summary>
/// Represents errors that occur during the execution of a specification.
/// </summary>
[Serializable]
public  class SpecException : Exception
{
    /// <summary>
    /// Initializes a new instance of the SpecException class.
    /// </summary>
    public SpecException()
    {
    }

    /// <summary>
    /// Initializes a new instance of the SpecException class with a specified error message.
    /// </summary>
    /// <param name="message">The message that describes the error.</param>
    public SpecException(string message)
        : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the SpecException class with a specified error message and a reference to the inner exception that is the cause of this exception.
    /// </summary>
    /// <param name="message">The error message that explains the reason for the exception.</param>
    /// <param name="innerException">The exception that is the cause of the current exception, or a null reference if no inner exception is specified.</param>
    public SpecException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}