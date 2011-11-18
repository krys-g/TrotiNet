using System;

namespace TrotiNet
{
    /// <summary>
    /// Container for a HTTP status line,
    /// i.e. the first line of a HTTP response
    /// </summary>
    public class HttpStatusLine
    {
        /// <summary>
        /// The version of the HTTP protocol.
        /// </summary>
        /// <remarks>
        /// For example, "1.1" means "HTTP/1.1"
        /// </remarks>
        public string ProtocolVersion { get; protected set; }

        /// <summary>
        /// The parsed HTTP status code
        /// </summary>
        /// <remarks>
        /// Integer value between 100 and 599 included
        /// </remarks>
        public int StatusCode { get; protected set; }

        /// <summary>
        /// Original status line as seen in the TCP stream
        /// </summary>
        public readonly string StatusLine;

        internal HttpStatusLine(HttpSocket hs)
        {
            string line;
            do
                line = hs.ReadAsciiLine().Trim();
            while (line.Length == 0);
            string[] items = line.Split(sp,
                StringSplitOptions.RemoveEmptyEntries);
            // Note: the status line has three items: the HTTP protocol
            // version, the status code, and the reason phrase.
            // Only the reason phrase may be empty.
            if (items.Length < 2)
                throw new HttpProtocolBroken("Unrecognized status line '" +
                    line + "'");

            ProtocolVersion = ParserHelper.ParseProtocolVersion(
                items[0]);

            string code = items[1];
            if (code.Length != 3 ||
                !char.IsDigit(code[0])) // we only test the first character
                throw new HttpProtocolBroken("Invalid status code '" +
                    code + "'");

            //string Reason = rest of the string; // we don't need it

            StatusCode = int.Parse(code);
            StatusLine = line;
        }
        readonly char[] sp = { ' ' };

        internal void SendTo(HttpSocket hs)
        {
            hs.WriteAsciiLine(StatusLine);
        }

        /// <summary>
        /// Return a string representation of the instance
        /// </summary>
        public override string ToString()
        {
            return StatusLine;
        }
    }
}
