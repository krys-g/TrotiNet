using System;
using System.Collections.Generic;
using System.Text;

namespace TrotiNet
{
    /// <summary>
    /// Container for a parsed copy of some headers relevant to the proxy,
    /// along with an unparsed copy of all the headers with their order
    /// unchanged
    /// </summary>
    public class HttpHeaders
    {
        enum HeaderType
        {
            Uint,
            String,
            Strings
        }

        /// <summary>
        /// Cache-Control header value
        /// </summary>
        public string CacheControl
        {
            get { return GetItem<string>("Cache-Control"); }
            set { SetItem("Cache-Control", value, HeaderType.String); }
        }

        /// <summary>
        /// Connection header value
        /// </summary>
        public string[] Connection
        {
            get { return GetItem<string[]>("connection"); }
            set { SetItem("Connection", value, HeaderType.Strings); }
        }

        /// <summary>
        /// Content-Encoding header value
        /// </summary>
        public string ContentEncoding
        {
            get { return GetItem<string>("content-encoding"); }
            set { SetItem("Content-Encoding", value, HeaderType.String); }
        }

        /// <summary>
        /// Content-Length header value
        /// </summary>
        public uint? ContentLength
        {
            get { return GetItem<uint?>("content-length"); }
            set { SetItem("Content-Length", value, HeaderType.Uint); }
        }

        /// <summary>
        /// Expires header value
        /// </summary>
        public string Expires
        {
            get { return GetItem<string>("Expires"); }
            set { SetItem("Expires", value, HeaderType.String); }
        }

        /// <summary>
        /// Pragma header value
        /// </summary>
        public string Pragma
        {
            get { return GetItem<string>("Pragma"); }
            set { SetItem("Pragma", value, HeaderType.String); }
        }

        /// <summary>
        /// Map { header name } -> { header string value }
        /// </summary>
        /// <remarks>
        /// Keys are stored in lower-case characters.
        /// Values have their spaces and trailing newlines trimmed.
        /// </remarks>
        public Dictionary<string, string> Headers { get; protected set; }

        /// <summary>
        /// HTTP headers as received (newline markers may have been fixed)
        /// </summary>
        /// <remarks>
        /// If RemoveHeader has been called, then HeadersInOrder will be
        /// modified. In particular, the header ordering may change.
        /// </remarks>
        public string HeadersInOrder { get; protected set; }

        /// <summary>
        /// Host header value
        /// </summary>
        public string Host
        {
            get { return GetItem<string>("host"); }
            set { SetItem("Host", value, HeaderType.String); }
        }

        /// <summary>
        /// Map { header name } -> { header parsed value }
        /// </summary>
        /// <remarks>
        /// Keys are stored in lower-case characters.
        /// </remarks>
        Dictionary<string, object> ParsedHeaders;

        /// <summary>
        /// Proxy-Connection header value
        /// </summary>
        /// <remarks>
        /// Proxy-Connection is not officially part of HTTP/1.1
        /// </remarks>
        public string[] ProxyConnection
        {
            get { return GetItem<string[]>("proxy-connection"); }
            set { SetItem("Proxy-Connection", value, HeaderType.Strings); }
        }

        /// <summary>
        /// Referer (sic) header value
        /// </summary>
        public string Referer
        {
            get { return GetItem<string>("referer"); }
            set { SetItem("Referer", value, HeaderType.String); }
        }

        /// <summary>
        /// Transfer-Encoding header value
        /// </summary>
        public string[] TransferEncoding
        {
            get { return GetItem<string[]>("transfer-encoding"); }
            set { SetItem("Transfer-Encoding", value, HeaderType.Strings); }
        }

        /// <summary>
        /// Instantiate empty HTTP headers
        /// </summary>
        public HttpHeaders()
        {
            Headers = new Dictionary<string, string>();
            ParsedHeaders = new Dictionary<string, object>();
        }

        /// <summary>
        /// Read and parse HTTP headers from a connected socket
        /// </summary>
        public HttpHeaders(HttpSocket source): this()
        {
            StringBuilder sb = new StringBuilder(512);

            while (true)
            {
                var line = source.ReadAsciiLine();
                if (line.Length == 0)
                    break;
                sb.Append(line);
                sb.Append("\r\n"); // Note: if the header newline was
                    // incorrectly formatted (i.e. LF instead of CRLF),
                    // we correct it here. This is one point where our
                    // proxy is not fully transparent.

                var iSplit = line.IndexOf(':');
                if (iSplit <= 0)
                    throw new HttpProtocolBroken("No colon in HTTP header");

                // Header names are case-insensitive, but only some header 
                // values are.
                string HeaderName = line.Substring(0, iSplit).Trim().ToLower();
                string HeaderValue = line.Substring(iSplit + 1).Trim();
                if (IsHeaderValueCaseInsensitive(HeaderName))
                    HeaderValue = HeaderValue.ToLower();

                string previous_value = null;
                if (Headers.TryGetValue(HeaderName, out previous_value))
                {
                    // Duplicate headers: concatenate them
                    // (RFC 2616, section 4.2)

                    // However, this should only occur if the value of that
                    // header is a comma-separated list. In the real world,
                    // it has been observed that headers with
                    // non-comma-separated values, such as Content-Length, *are*
                    // in some rare cases repeated, so we should not concatenate
                    // the values.
                    if (!HeaderName.Equals("content-length"))
                        Headers[HeaderName] = previous_value + "," + HeaderValue;
                }
                else
                    Headers[HeaderName] = HeaderValue;
            }

            HeadersInOrder = sb.ToString();

            // Parse a subset of the header values.
            // If headers are added, don't forget to update RemoveHeader
            // as well.
            Connection = ParseMultipleStringValues("connection");
            ContentEncoding = ParseStringValue("content-encoding");
            ContentLength = ParseIntValue("content-length");
            Host = ParseStringValue("host");
            ProxyConnection = ParseMultipleStringValues("proxy-connection");
            Referer = ParseStringValue("referer");
            TransferEncoding = ParseMultipleStringValues("transfer-encoding");
        }

        /// <summary>
        /// Initialize a new instance with the provided set of
        /// HTTP headers
        /// </summary>
        public HttpHeaders(Dictionary<string, string> headers)
        {
            throw new NotImplementedException();
        }

        T GetItem<T>(string header_name)
        {
            System.Diagnostics.Debug.Assert(header_name.Equals(
                header_name.ToLower()));
            object o = null;
            ParsedHeaders.TryGetValue(header_name, out o);
            return (T)o;
        }

        /// <summary>
        /// Parse a HTTP header that is expected to contain a decimal value
        /// </summary>
        /// <param name="HeaderName">
        /// The header name, in lower-case
        /// </param>
        protected uint? ParseIntValue(string HeaderName)
        {
            System.Diagnostics.Debug.Assert(HeaderName.Equals(
                HeaderName.ToLower()));

            try
            {
                string value = null;
                if (!Headers.TryGetValue(HeaderName, out value))
                    return null;
                return UInt32.Parse(value);
            }
            catch { return null; }
        }

        /// <summary>
        /// Split a HTTP header value along the commas, and trim the spaces
        /// </summary>
        /// <param name="HeaderName">
        /// The header name, in lower-case
        /// </param>
        protected string[] ParseMultipleStringValues(string HeaderName)
        {
            System.Diagnostics.Debug.Assert(HeaderName.Equals(
                HeaderName.ToLower()));

            try
            {
                string value = null;
                if (!Headers.TryGetValue(HeaderName, out value))
                    return null;

                string[] values = value.Split(',');
                for (int i = 0; i < values.Length; i++)
                    values[i] = values[i].Trim();
                return values;
            }
            catch { return null; }
        }

        /// <summary>
        /// Retrieve a header value and trim the spaces
        /// </summary>
        /// <param name="HeaderName">
        /// The header name, in lower-case
        /// </param>
        protected string ParseStringValue(string HeaderName)
        {
            System.Diagnostics.Debug.Assert(HeaderName.Equals(
                HeaderName.ToLower()));

            try
            {
                string value = null;
                if (!Headers.TryGetValue(HeaderName, out value))
                    return null;
                return value.Trim();
            }
            catch { return null; }
        }

        bool IsHeaderValueCaseInsensitive(string HeaderName)
        {
            // Note: some other headers may be case-insensitive, but
            // the ones listed here really have to be lowerized,
            // because TrotiNet assumes so.

            System.Diagnostics.Debug.Assert(HeaderName.Equals(
                HeaderName.ToLower()));

            return
                HeaderName.Equals("connection") ||
                HeaderName.Equals("content-encoding") ||
                HeaderName.Equals("proxy-connection") ||
                HeaderName.Equals("transfer-encoding");
        }

        internal void SendTo(HttpSocket hs)
        {
            hs.WriteAsciiLine(HeadersInOrder);
            // Note: HeadersInOrder contains one trailing newline, so
            // WriteAsciiLine() will send two newlines (which is what we
            // want).
        }

        void SetItem(string HeaderName, object value, HeaderType ht)
        {
            string header_name = HeaderName.ToLower();

            // Does the key exist already?
            bool bHasKey;
            {
                string dummy;
                bHasKey = Headers.TryGetValue(header_name, out dummy);
            }

            // Update Headers
            string s = null;
            if (value == null)
            {
                if (!bHasKey)
                    // Nothing to remove
                    return;
                Headers.Remove(header_name);
            }
            else
            {
                switch(ht)
                {
                    case HeaderType.Uint:
                        s = value.ToString(); break;
                    case HeaderType.String:
                        s = (string)value; break;
                    case HeaderType.Strings:
                        s = String.Join(";", (string[])value); break;
                    default:
                        System.Diagnostics.Debug.Assert(false);
                        break;
                }
                Headers[header_name] = s;
            }

            // Update ParsedHeaders
            ParsedHeaders[header_name] = value;

            // Update HeadersInOrder
            if (bHasKey)
            {
                string[] items = HeadersInOrder.Split(CRLF_a,
                    StringSplitOptions.RemoveEmptyEntries);
                StringBuilder sb = new StringBuilder(512);
                foreach (string item in items)
                {
                    var iSplit = item.IndexOf(':');
                    System.Diagnostics.Debug.Assert(iSplit > 0);
                    var hn = item.Substring(0, iSplit).Trim().ToLower();
                    if (hn.Equals(header_name))
                    {
                        if (s == null)
                            // Skip (= remove) the header
                            continue;

                        sb.Append(HeaderName);
                        sb.Append(": ");
                        sb.Append(s);
                    }
                    else
                        sb.Append(item);
                    sb.Append("\r\n");
                }
                HeadersInOrder = sb.ToString();
            }
            else
                HeadersInOrder += HeaderName + ": " + s + "\r\n";
        }

        static readonly string[] CRLF_a = { "\r\n" };
    }
}
