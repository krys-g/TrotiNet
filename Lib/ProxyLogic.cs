//#define DEBUG_EXTRA

using System;
using System.Net;
using System.Net.Sockets;
using log4net;

namespace TrotiNet
{
    /// <summary>
    /// Abstract class for all HTTP proxy logic implementations
    /// </summary>
    /// <remarks>
    /// One instance of a derived class will be created per client connection.
    /// </remarks>
    public abstract class AbstractProxyLogic
    {
        static readonly ILog log = Log.Get();

        /// <summary>
        /// Port to which <code>SocketBP</code> is currently connected
        /// </summary>
        protected int DestinationPort;

        /// <summary>
        /// Name of the host to which <code>SocketBP</code> is currently
        /// connected
        /// </summary>
        protected string DestinationHostName;

        /// <summary>
        /// Set to a proxy host name if our proxy is not connecting to
        /// the internet, but to another proxy instead
        /// </summary>
        protected string RelayHttpProxyHost;

        /// <summary>
        /// Set to a proxy bypass specification if our proxy is not connecting
        /// to the internet, but to another proxy instead
        /// </summary>
        /// <remarks>
        /// XXX Bypass not implemented
        /// </remarks>
        protected string RelayHttpProxyOverride;

        /// <summary>
        /// Set to a proxy port if our proxy is not connecting to
        /// the internet, but to another proxy instead
        /// </summary>
        protected int RelayHttpProxyPort;

        /// <summary>
        /// Socket dedicated to the (client) browser-proxy communication
        /// </summary>
        protected HttpSocket SocketBP;

        /// <summary>
        /// Socket dedicated to the proxy-server (remote) communication
        /// </summary>
        protected HttpSocket SocketPS;

        /// <summary>
        /// Common constructor for proxies; one proxy instance is created
        /// per client connection
        /// </summary>
        /// <param name="socketBP">Client socket</param>
        protected AbstractProxyLogic(HttpSocket socketBP)
        {
            System.Diagnostics.Debug.Assert(socketBP != null);
            SocketBP = socketBP;
            SocketPS = null;
        }

        /// <summary>
        /// If necessary, connect the remote <code>SocketPS</code> socket
        /// to the given host and port
        /// </summary>
        /// <param name="hostname">Remote host name</param>
        /// <param name="port">Remote port</param>
        /// <remarks>
        /// If SocketPS is already connected to the right host and port,
        /// the socket is reused as is.
        /// </remarks>
        protected void Connect(string hostname, int port)
        {
            System.Diagnostics.Debug.Assert(!String.IsNullOrEmpty(hostname));
            System.Diagnostics.Debug.Assert(port > 0);

            if (DestinationHostName != null &&
                DestinationHostName.Equals(hostname) &&
                DestinationPort == port &&
                (SocketPS != null && !SocketPS.IsSocketDead()))
                // Nothing to do, just reuse the socket
                return;

            if (SocketPS != null)
            {
                log.Debug("Changing hostname/port from " +
                    DestinationHostName + ":" + DestinationPort +
                    " to " + hostname + ":" + port);

                // We have a socket connected to the wrong host (or port)
                SocketPS.CloseSocket();
                SocketPS = null;
            }

            IPAddress[] ips = Dns.GetHostAddresses(hostname);
            Socket socket = null;
            Exception e = null;
            foreach (var ip in ips)
            {
                try
                {
                    socket = new Socket(ip.AddressFamily, SocketType.Stream,
                        ProtocolType.Tcp);
                    socket.Connect(ip, port);
                    break;
                }
                catch (Exception ee)
                {
                    if (ip.Equals(IPAddress.IPv6Loopback))
                        // Do not log that
                        continue;

                    if (e == null)
                        e = ee;
                    if (socket != null)
                    {
                        socket.Close();
                        socket = null;
                    }

                    log.Error(ee);
                }
            }
            if (socket == null)
                throw e;

            // Checked up, and good to go
            SocketPS = new HttpSocket(socket);
            DestinationHostName = hostname;
            DestinationPort = port;

            log.Debug("SocketPS connected to " + hostname + ":" + port);
        }

        /// <summary>
        /// Extract the host and port to use from either the HTTP request
        /// line, or the HTTP headers; update the request line to remove
        /// the hostname and port
        /// </summary>
        /// <param name="hrl">
        /// The HTTP request line; the URI will be updated to remove the
        /// host name and port number</param>
        /// <param name="hh_rq">The HTTP request headers</param>
        /// <param name="port">
        /// When this method returns, contains the request port
        /// </param>
        /// <remarks>
        /// May modify the URI of <code>hrl</code>
        /// </remarks>
        protected string ParseDestinationHostAndPort(
            HttpRequestLine hrl, HttpHeaders hh_rq, out int port)
        {
            string host = null;
            bool bIsConnect = hrl.Method.Equals("CONNECT");
            port = bIsConnect ? 443 : 80;

            bool bIsHTTP1_0 = hrl.ProtocolVersion.Equals("1.0");
            if (hrl.URI.Equals("*"))
            {
                System.Diagnostics.Debug.Assert(!bIsHTTP1_0);
                goto hostname_from_header;
            }

            // At this point, hrl.URI follows one of these forms:
            // - scheme:(//authority)/abs_path
            // - authority
            // - /abs_path

            int prefix = 0; // current parse position
            if (hrl.URI.Contains("://"))
            {
                if (hrl.URI.StartsWith("http://"))
                    prefix = 7; // length of "http://"
                else
                if (hrl.URI.StartsWith("https://"))
                {
                    prefix = 8; // length of "https://"
                    port = 443;
                }
                else
                throw new HttpProtocolBroken(
                    "Expected scheme missing or unsupported");
            }

            // Starting from offset prefix, we now have either:
            // 1) authority (only for CONNECT)
            // 2) authority/abs_path
            // 3) /abs_path

            int slash = hrl.URI.IndexOf('/', prefix);
            string authority = null;
            if (slash == -1)
            {
                // case 1
                authority = hrl.URI;
                System.Diagnostics.Debug.Assert(bIsConnect);
            }
            else
            if (slash > 0) // Strict inequality
                // case 2
                authority = hrl.URI.Substring(prefix, slash - prefix);

            if (authority != null)
            {
                // authority is either:
                // a) hostname
                // b) hostname:
                // c) hostname:port

                int c = authority.IndexOf(':');
                if (c < 0)
                    // case a)
                    host = authority;
                else
                if (c == authority.Length - 1)
                    // case b)
                    host = authority.TrimEnd('/');
                else
                {
                    // case c)
                    host = authority.Substring(0, c);
                    port = int.Parse(authority.Substring(c + 1));
                }

                prefix += authority.Length;
            }

            if (host != null)
            {
#if false
                // XXX Not sure whether this can happen (without doing ad
                // replacement) or if we want to prevent it
                if (hh_rq.Host != null)
                {
                    // Does hh_rq.Host and host match? (disregarding
                    // the potential ":port" prefix of hh_rq.Host)
                    int c2 = hh_rq.Host.IndexOf(':');
                    string rq_host = c2 < 0 ? hh_rq.Host :
                        hh_rq.Host.Substring(0, c2);
                    if (!rq_host.Equals(host))
                        // Host discrepancy: fix the 'Host' header
                        hh_rq.Host = host;
                }
#endif

                // Remove the host from the request URI, unless the "server"
                // is actually a proxy, in which case the URI should remain
                // unchanged. (RFC 2616, section 5.1.2)
                if (RelayHttpProxyHost == null)
                {
                    hrl.URI = hrl.URI.Substring(prefix);
                    log.Debug("Rewriting request line as: " +
                        hrl.RequestLine);
                }

                return host;
            }

            hostname_from_header:
            host = hh_rq.Host;
            if (host == null)
                throw new HttpProtocolBroken("No host specified");
            int cp = host.IndexOf(':');
            if (cp < 0) { /* nothing */ }
            else
            if (cp == host.Length - 1)
                host = host.TrimEnd('/');
            else
            {
                host = host.Substring(0, cp);
                port = int.Parse(host.Substring(cp + 1));
            }
            return host;
        }

        abstract internal bool LogicLoop();

        /// <summary>
        /// In case of a proxy chain, set the next proxy to contact
        /// </summary>
        /// <remarks>
        /// <code>ProxyOverride</code> is ignored.
        /// </remarks>
        public void SetRelayProxy(SystemProxySettings sps)
        {
            if (sps == null || !sps.ProxyEnable)
            {
                RelayHttpProxyHost = null;
                RelayHttpProxyPort = 0;
                return;
            }

            sps.GetHttpOnlyProxy(out RelayHttpProxyHost,
                out RelayHttpProxyPort);
            RelayHttpProxyOverride = null;
        }
    }

    /// <summary>
    /// Implement the full HTTP proxy logic for one browser connection
    /// </summary>
    public class BaseProxyLogic: AbstractProxyLogic
    {
        static readonly ILog log = Log.Get();

        internal BaseProxyLogic(HttpSocket socketBP): base(socketBP) { }

        /// <summary>
        /// Called when RequestLine and RequestHeaders are set
        /// </summary>
        virtual protected void OnReceiveRequest() { }

        /// <summary>
        /// Called when ResponseStatusLine and ResponseHeaders are set
        /// </summary>
        virtual protected void OnReceiveResponse() { }

        /// <summary>
        /// The request line of the HTTP request currently being handled
        /// </summary>
        protected HttpRequestLine RequestLine;

        /// <summary>
        /// The request headers of the HTTP request currently being handled
        /// </summary>
        protected HttpHeaders RequestHeaders;

        /// <summary>
        /// The response status line of the HTTP response received
        /// </summary>
        protected HttpStatusLine ResponseStatusLine;

        /// <summary>
        /// The response header line of the HTTP response received
        /// </summary>
        protected HttpHeaders ResponseHeaders;

        override internal bool LogicLoop()
        {
            bool bPersistConnection = false;
            try
            {
                try
                {
                    RequestLine = new HttpRequestLine(SocketBP);
                }
                catch (TrotiNet.IoBroken)
                {
                    // The request line is the first line of a HTTP request.
                    // If none comes in a timely fashion, then we eventually
                    // get a IoBroken exception, which is common enough
                    // not to be rethrown.
                    return false;
                }
                catch (SocketException)
                {
                    // Ditto
                    return false;
                }

                RequestHeaders = new HttpHeaders(SocketBP);

                if (RequestLine.Method.Equals("CONNECT"))
                {
                    log.Debug("Method CONNECT not implemented");
                    SocketBP.Send501();
                    return false;
                }

                log.Info("Got request " + RequestLine.RequestLine);
                OnReceiveRequest();

                if (RelayHttpProxyHost == null)
                {
                    int NewDestinationPort;
                    string NewDestinationHost = ParseDestinationHostAndPort(
                        RequestLine, RequestHeaders, out NewDestinationPort);
                    Connect(NewDestinationHost, NewDestinationPort);
                }
                else
                    Connect(RelayHttpProxyHost, RelayHttpProxyPort);

                // Find out whether the request has a message body
                // (RFC 2616, section 4.3); if it has, get the message length
                bool bRequestHasMessage = false;
                uint RequestMessageLength = 0;
                bool bRequestMessageChunked = false;
                if (RequestHeaders.TransferEncoding != null)
                {
                    bRequestHasMessage = true;
                    bRequestMessageChunked = Array.IndexOf<string>(
                     RequestHeaders.TransferEncoding, "chunked") >= 0;
                    System.Diagnostics.Debug.Assert(
                        bRequestMessageChunked);
                }
                else
                if (RequestHeaders.ContentLength != null)
                {
                    bRequestHasMessage = true;
                    RequestMessageLength = (uint)RequestHeaders.ContentLength;
                }

                bool bUseDefaultPersist = true;
                if (RequestHeaders.ProxyConnection != null)
                {
                    // Note: This is not part of the HTTP 1.1 standard. See
                    // http://homepage.ntlworld.com./jonathan.deboynepollard/
                    //                   FGA/web-proxy-connection-header.html
                    foreach (string i in RequestHeaders.ProxyConnection)
                    {
                        // Note: we might want to distinguish between persisting
                        // SocketBP (ProxyConnection) and SocketPS (Connection)
                        if (i.Equals("close"))
                        {
                            bPersistConnection = false;
                            bUseDefaultPersist = false;
                            break;
                        }
                        if (i.Equals("keep-alive"))
                        {
                            bPersistConnection = true;
                            bUseDefaultPersist = false;
                            break;
                        }
                    }
                    if (RelayHttpProxyHost == null)
                        RequestHeaders.ProxyConnection = null;
                }

                // Transmit the request to the server
                RequestLine.SendTo(SocketPS);
                RequestHeaders.SendTo(SocketPS);
                if (bRequestHasMessage)
                {
                    // Tunnel the request message
                    if (bRequestMessageChunked)
                        SocketBP.TunnelChunkedDataTo(SocketPS);
                    else
                    {
                        System.Diagnostics.Debug.Assert(
                            RequestMessageLength > 0);
                        SocketBP.TunnelDataTo(SocketPS, RequestMessageLength);
                    }
                }

#if DEBUG_EXTRA
                log.Debug("RH: " + RequestHeaders.HeadersInOrder);
#endif

                // Wait until we receive the response, then parse its header
                ResponseStatusLine = new HttpStatusLine(SocketPS);
                ResponseHeaders = new HttpHeaders(SocketPS);
                OnReceiveResponse();

#if DEBUG_EXTRA
                log.Debug("ASL: " + ResponseStatusLine.StatusLine);
                log.Debug("AH: " + ResponseHeaders.HeadersInOrder);
#endif

                // Update bPersistConnection (RFC 2616, section 14.10)
                if (RequestHeaders.Connection != null)
                    foreach (var item in RequestHeaders.Connection)
                    {
                        if (item.Equals("close"))
                        {
                            bPersistConnection = false;
                            bUseDefaultPersist = false;
                            break;
                        }
                        if (item.Equals("keep-alive"))
                        {
                            bPersistConnection = true;
                            bUseDefaultPersist = false;
                            break;
                        }
                    }
                if (ResponseHeaders.Connection != null)
                    foreach (var item in ResponseHeaders.Connection)
                    {
                        if (item.Equals("close"))
                        {
                            bPersistConnection = false;
                            bUseDefaultPersist = false;
                            break;
                        }
                        if (item.Equals("keep-alive"))
                        {
                            bPersistConnection = true;
                            bUseDefaultPersist = false;
                            break;
                        }
                    }
                if (bUseDefaultPersist)
                    bPersistConnection =
                        (!ResponseStatusLine.ProtocolVersion.Equals("1.0"));

                // Note: we do not remove fields mentioned in the
                //  'Connection' header (the specs say we should).

                if (bPersistConnection)
                    SocketPS.KeepAlive = true;

                // Transmit the response header to the client
                ResponseStatusLine.SendTo(SocketBP);
                ResponseHeaders.SendTo(SocketBP);

                // Find out if there is a message body
                // (RFC 2616, section 4.4)
                int sc = ResponseStatusLine.StatusCode;
                if (RequestLine.Method.Equals("HEAD") ||
                    sc == 204 || sc == 304 || (sc >= 100 && sc <= 199))
                    goto no_message_body;

                bool bResponseMessageChunked = false;
                uint ResponseMessageLength = 0;
                if (ResponseHeaders.TransferEncoding != null)
                {
                    bResponseMessageChunked = Array.IndexOf<string>(
                     ResponseHeaders.TransferEncoding,
                     "chunked") >= 0;
                    System.Diagnostics.Debug.Assert(
                        bResponseMessageChunked);
                }
                else
                if (ResponseHeaders.ContentLength != null)
                {
                    ResponseMessageLength =
                        (uint)ResponseHeaders.ContentLength;
                    if (ResponseMessageLength == 0)
                        goto no_message_body;
                }
                else
                {
                    // If the connection is not being closed,
                    // we need a content length.
                    System.Diagnostics.Debug.Assert(!bPersistConnection);
                }

                if (!bPersistConnection)
                    // Pipeline until the connection is closed
                    SocketPS.TunnelDataTo(SocketBP);
                else
                if (bResponseMessageChunked)
                    SocketPS.TunnelChunkedDataTo(SocketBP);
                else
                    SocketPS.TunnelDataTo(SocketBP, ResponseMessageLength);

            no_message_body: ;
            }
            catch (Exception e)
            {
                log.Error(e);
                bPersistConnection = false;
            }

            if (!bPersistConnection && SocketPS != null)
            {
                SocketPS.CloseSocket();
                SocketPS = null;
            }
            return bPersistConnection;
        }
    }

    /// <summary>
    /// Transparent proxy that tunnels HTTP requests (mostly) unchanged
    /// </summary>
    public class ProxyLogic : BaseProxyLogic
    {
        /// <summary>
        /// Instantiate a transparent proxy
        /// </summary>
        /// <param name="socketBP">Client browser-proxy socket</param>
        public ProxyLogic(HttpSocket socketBP): base(socketBP) { }

        /// <summary>
        /// Static constructor
        /// </summary>
        static public AbstractProxyLogic CreateProxy(HttpSocket socketBP)
        {
            return new ProxyLogic(socketBP);
        }

        /// <summary>
        /// Change the request URI; also change the 'Host' request header,
        /// when necessary
        /// </summary>
        /// <remarks>
        /// If required, this function should be called from
        /// <code>OnReceiveRequest</code>.
        /// </remarks>
        public void ChangeRequestURI(string newURI)
        {
            if (RequestLine == null)
                throw new RuntimeException("Request line not available");

            RequestLine.URI = newURI;

            if (RequestHeaders != null && RequestHeaders.Host != null)
            {
                // Extract the host from the URI
                int prefix = newURI.IndexOf("://");
                string s = (prefix < 0)
                    ? newURI
                    : newURI.Substring(prefix + 3);

                int i = s.IndexOf("/");
                if (i <= 0)
                    // No host in URI
                    return;
                int j = s.IndexOf(":", 0, i);
                if (j >= 0)
                    // Ignore the port number
                    i = j;
                string host = s.Substring(0, i);

                // Update the 'Host' HTTP header
                RequestHeaders.Host = host;
            }
        }
    }

    /// <summary>
    /// Dummy proxy that simply echoes back what it gets from the browser
    /// </summary>
    /// Used for TCP testing.
    public class ProxyDummyEcho : AbstractProxyLogic
    {
        bool bPrintEchoPrefix;

        /// <summary>
        /// Instantiate a dummy proxy that echoes what it reads on the
        /// socket back to it
        /// </summary>
        /// <param name="socketBP">Client socket</param>
        /// <param name="PrintEchoPrefix">If true, the proxy will add an
        /// "Echo" prefix for each message</param>
        public ProxyDummyEcho(HttpSocket socketBP, bool PrintEchoPrefix):
            base(socketBP)
        {
            bPrintEchoPrefix = PrintEchoPrefix;
        }

        /// <summary>
        /// Static constructor with <code>PrintEchoPrefix = true</code>
        /// </summary>
        static public AbstractProxyLogic CreateEchoProxy(HttpSocket socketBP)
        {
            return new ProxyDummyEcho(socketBP, true);
        }

        /// <summary>
        /// Static constructor with <code>PrintEchoPrefix = false</code>
        /// </summary>
        static public AbstractProxyLogic CreateMirrorProxy(HttpSocket socketBP)
        {
            return new ProxyDummyEcho(socketBP, false);
        }

        override internal bool LogicLoop()
        {
            uint r = SocketBP.ReadBinary();
            if (r == 0)
                // Connection closed
                return false;

            string s = System.Text.ASCIIEncoding.ASCII.GetString(
                SocketBP.Buffer, 0, (int)r);
            if (bPrintEchoPrefix)
                SocketBP.WriteBinary(System.Text.ASCIIEncoding.
                    ASCII.GetBytes("Echo: "));
            SocketBP.WriteBinary(SocketBP.Buffer, r);

            if (s.StartsWith("x"))
                return false;
            return true;
        }
    }
}
