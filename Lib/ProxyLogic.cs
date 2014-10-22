namespace TrotiNet
{
    using System;
    using System.Linq;
    using System.IO;
    using System.IO.Compression;
    using System.Net;
    using System.Net.Sockets;
    using System.Text;
    using log4net;
    using System.Threading;

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
        /// Port to which <c>SocketBP</c> is currently connected
        /// </summary>
        protected int DestinationPort;

        /// <summary>
        /// Name of the host to which <c>SocketBP</c> is currently
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
        /// If necessary, connect the remote <c>SocketPS</c> socket
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

            IPAddress[] ips = Resolve(hostname);
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

        private static IPAddress[] Resolve(string hostname)
        {
            IPAddress[] ips = Dns.GetHostAddresses(hostname);
            return ips;
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
        /// May modify the URI of <c>hrl</c>
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

        /// <summary>
        /// Entry point to HTTP request handling
        /// </summary>
        abstract public bool LogicLoop();

        /// <summary>
        /// In case of a proxy chain, set the next proxy to contact
        /// </summary>
        /// <remarks>
        /// <c>ProxyOverride</c> is ignored.
        /// </remarks>
        public void SetRelayProxy(SystemProxySettings sps)
        {
            if (sps == null || !sps.ProxyEnable)
            {
                RelayHttpProxyHost = null;
                RelayHttpProxyPort = 0;
                return;
            }

            sps.GetHttpSpecificProxy(out RelayHttpProxyHost,
                out RelayHttpProxyPort);
            RelayHttpProxyOverride = null;
        }

        /// <summary>
        /// Message packet handler for tunneling data from PS to BP
        /// </summary>
        protected void TunnelBP(byte[] msg, uint position, uint to_send)
        {
            if (to_send == 0)
                return;
            if (SocketBP.WriteBinary(msg, position, to_send) < to_send)
                throw new IoBroken();
        }

        /// <summary>
        /// Message packet handler for tunneling data from BP to PS
        /// </summary>
        protected void TunnelPS(byte[] msg, uint position, uint to_send)
        {
            if (to_send == 0)
                return;
            if (SocketPS.WriteBinary(msg, position, to_send) < to_send)
                throw new IoBroken();
        }
    }

    /// <summary>
    /// Implement the full HTTP proxy logic for one browser connection
    /// </summary>
    public class BaseProxyLogic: AbstractProxyLogic
    {
        static readonly ILog log = Log.Get();

        /// <summary>
        /// Base proxy constructor (an arbitrary intermediate step between
        /// AbstractProxyLogic, and ProxyLogic)
        /// </summary>
        public BaseProxyLogic(HttpSocket socketBP) : base(socketBP) { }

        /// <summary>
        /// Continuation delegate used in the request processing pipeline
        /// </summary>
        protected delegate void ProcessingStep();

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

        /// <summary>
        /// Maintains the internal state for the request currently being
        /// processed
        /// </summary>
        protected class RequestProcessingState
        {
            /// <summary>
            /// Whether the BP connection should be kept alive after handling
            /// the current request, or closed
            /// </summary>
            public bool bPersistConnectionBP;

            /// <summary>
            /// Whether the PS connection should be kept alive after handling
            /// the current request, or closed
            /// </summary>
            /// <remarks>
            /// If set to false, then <c>bPersistConnectionBP</c> will also be
            /// set to false, because if no Content-Length has been specified,
            /// the browser would keep on waiting (BP kept-alive, but PS
            /// closed).
            /// </remarks>
            public bool bPersistConnectionPS;

            /// <summary>
            /// Whether the request contains a message
            /// </summary>
            public bool bRequestHasMessage;

            /// <summary>
            /// Length of the request message, if any
            /// </summary>
            public uint RequestMessageLength;

            /// <summary>
            /// Whether the request message (if any) is being transmitted
            /// in chunks
            /// </summary>
            public bool bRequestMessageChunked;

            /// <summary>
            /// Set to true if no instruction was given in the request headers
            /// about whether the BP connection should persist
            /// </summary>
            public bool bUseDefaultPersistBP;

            /// <summary>
            /// When set to not null, will be called every time a raw fragment
            /// of a non-empty response message body is received; note that the
            /// packet handler becomes responsible for sending the response
            /// (whatever it is) to SocketBP
            /// </summary>
            /// <remarks>
            /// The message body might be compressed (or otherwise modified),
            /// as specified by the Content-Encoding header. Applications
            /// should use <c>ProxyLogic.GetResponseMessageStream</c> to
            /// decompress (whenever necessary) the message stream.
            /// </remarks>
            public HttpSocket.MessagePacketHandler OnResponseMessagePacket;

            /// <summary>
            /// Points to the next processing step; must be updated after
            /// each processing step, setting it to null will stop the
            /// processing
            /// </summary>
            public ProcessingStep NextStep;

            /// <summary>
            /// Processing state constructor
            /// </summary>
            /// <param name="StartStep">
            /// First step of the request processing pipeline
            /// </param>
            public RequestProcessingState(ProcessingStep StartStep)
            {
                NextStep = StartStep;
            }
        };

        /// <summary>
        /// Request processing pipeline state
        /// </summary>
        /// <seealso cref="RequestProcessingState"/>
        protected RequestProcessingState State;

        /// <summary>
        /// Pipeline step: close the connections and stop
        /// </summary>
        protected void AbortRequest()
        {
            if (SocketPS != null)
            {
                SocketPS.CloseSocket();
                SocketPS = null;
            }
            State.bPersistConnectionBP = false;
            State.NextStep = null;
        }

        /// <summary>
        /// Implement a base proxy logic. The procedure is called for each
        /// request as long as it returns true.
        /// </summary>
        override public bool LogicLoop()
        {
            // In order to enable derived classes to divert the standard
            // HTTP request processing in the most flexible way, the processing
            // is done in a continuation-passing way. That means each step
            // is responsible for updating State.NextStep, as appropriate.

            try
            {
                State = new RequestProcessingState(ReadRequest);
                while (State.NextStep != null)
                    State.NextStep();

                return State.bPersistConnectionBP;
            }
            catch
            {
                AbortRequest();
                throw;
            }
        }

        /// <summary>
        /// Called when RequestLine and RequestHeaders are set
        /// </summary>
        /// <remarks>
        /// May be used to override State.NextStep
        /// </remarks>
        virtual protected void OnReceiveRequest() { }

        /// <summary>
        /// Called when ResponseStatusLine and ResponseHeaders are set
        /// </summary>
        /// <remarks>
        /// May be used to override State.NextStep
        /// </remarks>
        virtual protected void OnReceiveResponse() { }

        /// <summary>
        /// Pipeline step: read the HTTP request from the client, schedule
        /// the next step to be <c>SendRequest</c>, and call
        /// <c>OnReceiveRequest</c>
        /// </summary>
        protected virtual void ReadRequest()
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
                AbortRequest();
                return;
            }
            catch (SocketException)
            {
                // Ditto
                AbortRequest();
                return;
            }

            RequestHeaders = new HttpHeaders(SocketBP);

            log.Info("Got request " + RequestLine.RequestLine);

            // We call OnReceiveRequest now because Connect() will
            // modify the request URI.
            State.NextStep = SendRequest;
            OnReceiveRequest();

            if (RequestLine.Method.Equals("CONNECT"))
            {
                HandleConnect();
            }

            // Now we parse the request to:
            // 1) find out where we should connect
            // 2) find out whether there is a message body in the request
            // 3) find out whether the BP connection should be kept-alive
            if (State.NextStep != null)
            {
                // Step 1)
                if (RelayHttpProxyHost == null)
                {
                    int NewDestinationPort;
                    string NewDestinationHost = ParseDestinationHostAndPort(
                        RequestLine, RequestHeaders, out NewDestinationPort);
                    Connect(NewDestinationHost, NewDestinationPort);
                }
                else
                    Connect(RelayHttpProxyHost, RelayHttpProxyPort);

                // Step 2)
                // Find out whether the request has a message body
                // (RFC 2616, section 4.3); if it has, get the message length
                State.bRequestHasMessage = false;
                State.RequestMessageLength = 0;
                State.bRequestMessageChunked = false;
                if (RequestHeaders.TransferEncoding != null)
                {
                    State.bRequestHasMessage = true;
                    State.bRequestMessageChunked = Array.IndexOf<string>(
                        RequestHeaders.TransferEncoding, "chunked") >= 0;
                    System.Diagnostics.Debug.Assert(
                        State.bRequestMessageChunked);
                }
                else
                if (RequestHeaders.ContentLength != null)
                {
                    State.RequestMessageLength =
                        (uint)RequestHeaders.ContentLength;

                    // Note: HTTP 1.0 wants "Content-Length: 0" when there
                    // is no entity body. (RFC 1945, section 7.2)
                    if (State.RequestMessageLength > 0)
                        State.bRequestHasMessage = true;
                }
            }
            // Step 3)
            State.bUseDefaultPersistBP = true;
            if (RequestHeaders.ProxyConnection != null)
            {
                // Note: This is not part of the HTTP 1.1 standard. See
                // http://homepage.ntlworld.com./jonathan.deboynepollard/FGA/web-proxy-connection-header.html
                foreach (string i in RequestHeaders.ProxyConnection)
                {
                    if (i.Equals("close"))
                    {
                        State.bPersistConnectionBP = false;
                        State.bUseDefaultPersistBP = false;
                        break;
                    }
                    if (i.Equals("keep-alive"))
                    {
                        State.bPersistConnectionBP = true;
                        State.bUseDefaultPersistBP = false;
                        break;
                    }
                }
                if (RelayHttpProxyHost == null)
                    RequestHeaders.ProxyConnection = null;
            }

            // Note: we do not remove fields mentioned in the
            //  'Connection' header (the specs say we should).

        }

        /// <summary>
        /// A specific case for the CONNECT command,
        /// connect both ends blindly (will work for HTTPS, SSH and others)
        /// </summary>
        virtual protected void HandleConnect()
        {
            int NewDestinationPort;
            string NewDestinationHost = ParseDestinationHostAndPort(
                RequestLine, RequestHeaders, out NewDestinationPort);
            Connect(NewDestinationHost, NewDestinationPort);
            this.State.NextStep = null;
            this.SocketBP.WriteAsciiLine(string.Format("HTTP/{0} 200 Connection established", RequestLine.ProtocolVersion));
            this.SocketBP.WriteAsciiLine(string.Empty);
            var socketsToConnect = new[] { this.SocketBP, this.SocketPS };

            socketsToConnect
                .Zip(socketsToConnect.Reverse(), (from, to) => new { from,to })
                .AsParallel()
                .ForAll(team => team.from.TunnelDataTo(team.to));
        }

        /// <summary>
        /// Pipeline step: tunnel the request from the client to the remove
        /// server, and schedule the next step to be <c>ReadResponse</c>
        /// </summary>
        protected virtual void SendRequest()
        {
            // Transmit the request to the server
            RequestLine.SendTo(SocketPS);
            RequestHeaders.SendTo(SocketPS);
            if (State.bRequestHasMessage)
            {
                // Tunnel the request message
                if (State.bRequestMessageChunked)
                    SocketBP.TunnelChunkedDataTo(SocketPS);
                else
                {
                    System.Diagnostics.Debug.Assert(
                        State.RequestMessageLength > 0);
                    SocketBP.TunnelDataTo(TunnelPS, State.RequestMessageLength);
                }
            }

            State.NextStep = ReadResponse;
        }

        /// <summary>
        /// Pipeline step: read the HTTP response from the local client,
        /// schedule the next step to be <c>SendResponse</c>, and call
        /// <c>OnReceiveResponse</c>
        /// </summary>
        protected virtual void ReadResponse()
        {
            // Wait until we receive the response, then parse its header
            ResponseStatusLine = new HttpStatusLine(SocketPS);
            ResponseHeaders = new HttpHeaders(SocketPS);

            // Get bPersistConnectionPS (RFC 2616, section 14.10)
            bool bUseDefaultPersistPS = true;
            if (ResponseHeaders.Connection != null)
                foreach (var item in ResponseHeaders.Connection)
                {
                    if (item.Equals("close"))
                    {
                        State.bPersistConnectionPS = false;
                        bUseDefaultPersistPS = false;
                        break;
                    }
                    if (item.Equals("keep-alive"))
                    {
                        State.bPersistConnectionPS = true;
                        bUseDefaultPersistPS = false;
                        break;
                    }
                }
            if (bUseDefaultPersistPS)
                State.bPersistConnectionPS =
                    (!ResponseStatusLine.ProtocolVersion.Equals("1.0"));

            if (State.bPersistConnectionPS)
                SocketPS.KeepAlive = true;
            else
                State.bPersistConnectionBP = false;

            State.NextStep = SendResponse;
            OnReceiveResponse();
        }

        /// <summary>
        /// Pipeline: tunnel the HTTP response from the remote server to the
        /// local client, and end the request processing
        /// </summary>
        protected virtual void SendResponse()
        {
            if (!(ResponseHeaders.TransferEncoding == null &&
                  ResponseHeaders.ContentLength == null))
            {
                // Transmit the response header to the client
                SendResponseStatusAndHeaders();
            }

            // Find out if there is a message body
            // (RFC 2616, section 4.4)
            int sc = ResponseStatusLine.StatusCode;
            if (RequestLine.Method.Equals("HEAD") ||
                sc == 204 || sc == 304 || (sc >= 100 && sc <= 199))
            {
                SendResponseStatusAndHeaders();
                goto no_message_body;
            }

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
                    // We really should have been given a response
                    // length. It appears that some popular websites
                    // send small files without a transfer-encoding
                    // or length.

                    // It seems that all of the browsers handle this
                    // case so we need to as well.

                    byte[] buffer = new byte[512];
                    SocketPS.TunnelDataTo(ref buffer);

                    // Transmit the response header to the client
                    ResponseHeaders.ContentLength = (uint)buffer.Length;
                    ResponseStatusLine.SendTo(SocketBP);
                    ResponseHeaders.SendTo(SocketBP);

                    SocketBP.TunnelDataTo(TunnelBP, buffer);
                    State.NextStep = null;
                    return;
                }

            if (State.OnResponseMessagePacket != null)
            {
                if (!State.bPersistConnectionPS)
                    // Pipeline until the connection is closed
                    SocketPS.TunnelDataTo(State.OnResponseMessagePacket);
                else
                    if (bResponseMessageChunked)
                        SocketPS.TunnelChunkedDataTo(
                            State.OnResponseMessagePacket);
                    else
                        SocketPS.TunnelDataTo(State.OnResponseMessagePacket,
                            ResponseMessageLength);
                State.OnResponseMessagePacket(null, 0, 0);
            }
            else
            {
                if (!State.bPersistConnectionPS)
                    // Pipeline until the connection is closed
                    SocketPS.TunnelDataTo(TunnelBP);
                else
                    if (bResponseMessageChunked)
                        SocketPS.TunnelChunkedDataTo(SocketBP);
                    else
                        SocketPS.TunnelDataTo(TunnelBP, ResponseMessageLength);
            }

        no_message_body:

            if (!State.bPersistConnectionPS && SocketPS != null)
            {
                SocketPS.CloseSocket();
                SocketPS = null;
            }

            State.NextStep = null;
        }

        /// <summary>
        /// Send the response status line and headers from the proxy to
        /// the client
        /// </summary>
        protected void SendResponseStatusAndHeaders()
        {
            ResponseStatusLine.SendTo(SocketBP);
            ResponseHeaders.SendTo(SocketBP);
        }
    }

    /// <summary>
    /// Wrapper around BaseProxyLogic that adds various utility functions
    /// </summary>
    public class ProxyLogic: BaseProxyLogic
    {
        /// <summary>
        /// Instantiate a transparent proxy
        /// </summary>
        /// <param name="socketBP">Client browser-proxy socket</param>
        public ProxyLogic(HttpSocket socketBP) : base(socketBP) { }

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
        /// <c>OnReceiveRequest</c>.
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

        /// <summary>
        /// Download the chunked file and return the byte array
        /// </summary>
        byte[] GetChunkedContent()
        {
            char[] c_ChunkSizeEnd = { ' ', ';' };
            MemoryStream chunked_stream = new MemoryStream();

            // (RFC 2616, sections 3.6.1, 19.4.6)
            while (true)
            {
                string chunk_header = SocketPS.ReadAsciiLine();

                if (string.IsNullOrEmpty(chunk_header))
                    continue;

                int sc = chunk_header.IndexOfAny(c_ChunkSizeEnd);
                string hexa_size;
                if (sc > -1)
                    // We have chunk extensions: ignore them
                    hexa_size = chunk_header.Substring(0, sc);
                else
                    hexa_size = chunk_header;

                uint size;
                try
                {
                    size = Convert.ToUInt32(hexa_size, 16);
                }
                catch
                {
                    string s = chunk_header.Length > 20
                        ? (chunk_header.Substring(0, 17) + "...")
                        : chunk_header;
                    throw new HttpProtocolBroken(
                        "Could not parse chunk size in: " + s);
                }

                if (size == 0)
                    break;

                byte[] buffer = new byte[size];
                SocketPS.TunnelDataTo(buffer, size);

                chunked_stream.Write(buffer, 0, (int)size);
            }

            return chunked_stream.ToArray();
        }

        /// <summary>
        /// Get a file with a known file size (i.e., not chunked).
        /// </summary>
        byte[] GetNonChunkedContent()
        {
            // Find out if there is a message body
            // (RFC 2616, section 4.4)
            int sc = ResponseStatusLine.StatusCode;
            if (RequestLine.Method.Equals("HEAD") ||
                sc == 204 || sc == 304 || (sc >= 100 && sc <= 199))
                return new byte[0];

            bool bResponseMessageChunked = false;
            uint ResponseMessageLength = 0;
            if (ResponseHeaders.TransferEncoding != null)
            {
                bResponseMessageChunked = Array.IndexOf<string>(
                    ResponseHeaders.TransferEncoding, "chunked") >= 0;
                System.Diagnostics.Debug.Assert(bResponseMessageChunked);
                if (bResponseMessageChunked)
                    throw new TrotiNet.RuntimeException(
                        "Chunked data found when not expected");
            }

            if (ResponseHeaders.ContentLength != null)
            {
                ResponseMessageLength =
                    (uint)ResponseHeaders.ContentLength;

                if (ResponseMessageLength == 0)
                    return new byte[0];
            }
            else
            {
                // If the connection is not being closed,
                // we need a content length.
                System.Diagnostics.Debug.Assert(
                    !State.bPersistConnectionPS);
            }

            byte[] buffer = new byte[ResponseMessageLength];
            SocketPS.TunnelDataTo(buffer, ResponseMessageLength);

            return buffer;
        }

        /// <summary>
        /// If this method is called on a response, either the custom
        /// response pipeline or the 302 redirect MUST be used.
        /// </summary>
        protected byte[] GetContent()
        {
            byte[] content = null;

            if (ResponseHeaders.TransferEncoding != null &&
                Array.IndexOf<string>(ResponseHeaders.TransferEncoding,
                    "chunked") >= 0)
                content = GetChunkedContent();
            else
                content = GetNonChunkedContent();

            return (content ?? new byte[0]);
        }

        /// <summary>
        /// Interpret a message with respect to its content encoding
        /// </summary>
        public Stream GetResponseMessageStream(byte[] msg)
        {
            Stream inS = new MemoryStream(msg);
            return GetResponseMessageStream(inS);
        }

        /// <summary>
        /// Interpret a message with respect to its content encoding
        /// </summary>
        public Stream GetResponseMessageStream(Stream inS)
        {
            Stream outS = null;
            string ce = ResponseHeaders.ContentEncoding;
            if (!String.IsNullOrEmpty(ce))
            {
                if (ce.StartsWith("deflate"))
                    outS = new DeflateStream(inS, CompressionMode.Decompress);
                else
                    if (ce.StartsWith("gzip"))
                        outS = new GZipStream(inS, CompressionMode.Decompress);
                    else
                        if (!ce.StartsWith("identity"))
                            throw new TrotiNet.RuntimeException(
                                "Unsupported Content-Encoding '" + ce + "'");
            }

            if (outS == null)
                return inS;
            return outS;
        }

        /// <summary>
        /// Compress a byte array based on the content encoding header
        /// </summary>
        /// <param name="output">The content to be compressed</param>
        /// <returns>The compressed content</returns>
        public byte[] CompressResponse(byte[] output)
        {
            string ce = ResponseHeaders.ContentEncoding;
            if (!String.IsNullOrEmpty(ce))
            {
                if (ce.StartsWith("deflate"))
                {
                    using (MemoryStream ms = new MemoryStream())
                    {
                        using (DeflateStream ds = new DeflateStream(ms,
                            CompressionMode.Compress, true))
                        {
                            ds.Write(output, 0, output.Length);
                            ds.Close();
                        }
                        return ms.ToArray();
                    }
                }
                else
                {
                    if (ce.StartsWith("gzip"))
                    {
                        using (MemoryStream ms = new MemoryStream())
                        {
                            using (GZipStream gs = new GZipStream(ms,
                                CompressionMode.Compress, true))
                            {
                                gs.Write(output, 0, output.Length);
                                gs.Close();
                            }
                            return ms.ToArray();
                        }
                    }
                    else
                    if (!ce.StartsWith("identity"))
                        throw new TrotiNet.RuntimeException(
                            "Unsupported Content-Encoding '" + ce + "'");
                }
            }

            return output;
        }

        /// <summary>
        /// Get an encoded byte array for a given string
        /// </summary>
        public byte[] EncodeStringResponse(string s, Encoding encoding)
        {
            byte[] output = encoding.GetBytes(s);
            return CompressResponse(output);
        }
    }

}
