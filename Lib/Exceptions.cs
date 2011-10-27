using System;

namespace TrotiNet
{
    /// <summary>
    /// Exception base class
    /// </summary>
    public abstract class HttpException : Exception
    {
        internal HttpException() : base() { }

        internal HttpException(string msg) : base(msg) { }
    }

    /// <summary>
    /// Thrown to report a Read/Write failure on the HttpSocket
    /// </summary>
    public class IoBroken : HttpException { }

    /// <summary>
    /// Thrown when the HTTP data received is not valid
    /// </summary>
    public class HttpProtocolBroken : HttpException
    {
        internal HttpProtocolBroken(string msg): base(msg) { }
    }

    /// <summary>
    /// Run-time library exception
    /// </summary>
    public class RuntimeException : Exception
    {
        internal RuntimeException(string msg) : base(msg) { }
    }
}
