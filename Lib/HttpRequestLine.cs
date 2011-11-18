using System;

namespace TrotiNet
{
    internal static class ParserHelper
    {
        static public string ParseProtocolVersion(string HttpProtocol)
        {
            if (!HttpProtocol.StartsWith("HTTP/"))
                throw new HttpProtocolBroken("Unrecognized HTTP version '" +
                    HttpProtocol);
            string version = HttpProtocol.Substring(5);
            if (version.IndexOf('.') < 0 || !char.IsDigit(version[0]))
                throw new HttpProtocolBroken("Invalid HTTP version '" +
                    HttpProtocol);
            return version;
        }
    }

    /// <summary>
    /// Container for a HTTP request line,
    /// i.e. the first line of a HTTP request
    /// </summary>
    public class HttpRequestLine
    {
        string _method;
        string _protocol_version;
        string _uri;

        /// <summary>
        /// HTTP method (e.g. "GET", "POST", etc.)
        /// </summary>
        /// <remarks>
        /// This field contains what has been received on the socket, and
        /// therefore can contain anything, including methods not mentioned
        /// in the HTTP protocol.
        /// Method is case-sensitive (RFC 2616, section 5.1.1).
        /// </remarks>
        public string Method
        {
            get { return _method; }
            set { _method = value; UpdateRequestLine(); }
        }

        /// <summary>
        /// The version of the HTTP protocol.
        /// </summary>
        /// <remarks>
        /// For example, "1.1" means "HTTP/1.1"
        /// </remarks>
        public string ProtocolVersion
        {
            get { return _protocol_version; }
            set { _protocol_version = value; UpdateRequestLine(); }
        }

        /// <summary>
        /// Target URI as seen in the TCP stream
        /// </summary>
        public string URI
        {
            get { return _uri; }
            set { _uri = value; UpdateRequestLine(); }
        }

        /// <summary>
        /// Original request line as seen in the TCP stream
        /// </summary>
        public string RequestLine { get; protected set; }

        internal HttpRequestLine(HttpSocket hs)
        {
            string line;
            do
                line = hs.ReadAsciiLine().Trim();
            while (line.Length == 0);

            string[] items = line.Split(sp,
                StringSplitOptions.RemoveEmptyEntries);
            if (items.Length != 3)
                throw new HttpProtocolBroken("Unrecognized request line '" +
                    line + "'");

            RequestLine = line;
            Method = items[0];
            URI = items[1];
            ProtocolVersion = ParserHelper.ParseProtocolVersion(items[2]);
        }
        readonly char[] sp = { ' ' };

        internal void SendTo(HttpSocket hs)
        {
            hs.WriteAsciiLine(RequestLine);
        }

        /// <summary>
        /// Return a string representation of the instance
        /// </summary>
        public override string ToString()
        {
            return RequestLine;
        }

        void UpdateRequestLine()
        {
            RequestLine = Method + " " + URI + " HTTP/" + ProtocolVersion;
        }
    }
}
