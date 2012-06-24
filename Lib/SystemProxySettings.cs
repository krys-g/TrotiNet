using System;

namespace TrotiNet
{
    /// <summary>
    /// Container class for proxy settings
    /// </summary>
    public class SystemProxySettings
    {
        /// <summary>
        /// System/IE option "Use a proxy server for your
        /// LAN (These settings will not apply to dial-up or VPN connections)."
        /// </summary>
        public bool ProxyEnable
        {
            get { return (proxyEnable == 1); }
            set { proxyEnable = value ? 1 : 0; }
        }
        int proxyEnable;

        /// <summary>
        /// Correspond to the system/IE advanced option "Exceptions" (do not
        /// use proxy server for these address prefixes). For example: *.local
        /// </summary>
        /// <remarks>
        /// If the option "Bypass proxy server for local addresses" is
        /// checked, ";&lt;local&gt;" is appended to ProxyOverride.
        /// </remarks>
        public string ProxyOverride;

        /// <summary>
        /// Correspond to the system/IE options "Address" and "Port", and the
        /// advanced option "Servers".
        /// </summary>
        /// <remarks>
        /// - If the proxy is the same for all protocols, use:
        ///   localhost:2000
        /// - If the proxy depends on the TCP service, use this syntax:
        ///   ftp=ip1:2002;http=ip2:2000;https=ip3:2001;socks=ip4:2003
        /// </remarks>
        public string ProxyServer;

        /// <summary>
        /// Correspond to the system/IE advanced option "Use Automatic
        /// Configuration Script."  
        /// </summary>
        public string AutoConfigURL;

        /// <summary>
        /// Constructor
        /// </summary>
        public SystemProxySettings(bool proxyEnable,
            string proxyServer, string proxyOverride, string autoConfigURL)
        {
            ProxyEnable = proxyEnable;
            ProxyServer = proxyServer;
            ProxyOverride = proxyOverride;
            AutoConfigURL = autoConfigURL;
        }

        /// <summary>
        /// Constructor with default (empty) auto config URL
        /// </summary>
        public SystemProxySettings(bool proxyEnable,
            string proxyServer, string proxyOverride) :
            this(proxyEnable, proxyServer, proxyOverride, null)
        {
        }

        /// <summary>
        /// Retrieve the HTTP-specific proxy settings
        /// </summary>
        public void GetHttpSpecificProxy(out string proxy_host,
            out int proxy_port)
        {
            GetProtocolSpecificProxy("http=", 80, out proxy_host,
                out proxy_port);
        }

        /// <summary>
        /// Retrieve the HTTP-specific proxy settings
        /// </summary>
        public void GetHttpsSpecificProxy(out string proxy_host,
            out int proxy_port)
        {
            GetProtocolSpecificProxy("https=", 443, out proxy_host,
                out proxy_port);
        }

        /// <summary>
        /// Extract protocol-specific proxy settings
        /// </summary>
        /// <param name="protocol">
        /// One of "ftp=", "socks=", "http=", "https="; it must end with a
        /// '=' sign.
        /// </param>
        /// <param name="default_port">
        /// The default port for the protocol, e.g. 80 for HTTP
        /// </param>
        /// <param name="proxy_host">
        /// Will be set to the proxy host name
        /// </param>
        /// <param name="proxy_port">
        /// Will be set to the proxy port
        /// </param>
        void GetProtocolSpecificProxy(string protocol, int default_port,
            out string proxy_host, out int proxy_port)
        {
            System.Diagnostics.Debug.Assert(
                protocol[protocol.Length - 1] == '=');

            proxy_host = null;
            proxy_port = 0;

            if (!ProxyEnable)
                return;

            if (String.IsNullOrEmpty(ProxyServer))
                return;

            // Recall that ProxyServer can have one of these two forms:
            //   [http=]localhost:2000
            //   ftp=ip1:2002;http=ip2:2000;https=ip3:2001;socks=ip4:2003
            string ProtocolProxyServer = null;
            if (ProxyServer.IndexOf(';') > -1)
            {
                // Find the protocol-specific part
                var items = ProxyServer.Split(';');
                for (int i = 0; i < items.Length; i++)
                    if (items[i].StartsWith(protocol))
                    {
                        ProtocolProxyServer = items[i];
                        break;
                    }
                if (ProtocolProxyServer == null)
                    // We didn't find a corresponding entry
                    return;
            }
            else
                // Either "<host>[:<port>]", or "<protocol><host>[:<port>]"
                ProtocolProxyServer = ProxyServer;

            // Again, we have "<host>[:<port>]" or "<protocol><host>[:<port>]"
            if (ProtocolProxyServer.IndexOf('=') > -1)
            {
                // We have "<protocol><host>[:<port>]".
                // Does the "<protocol>" prefix match?
                if (ProtocolProxyServer.StartsWith(protocol))
                    ProtocolProxyServer = ProtocolProxyServer.Substring(
                        protocol.Length);
                else
                    // The entry is for another protocol
                    return;
            }

            // Now we only have "<host>[:<port>]"
            var c = ProtocolProxyServer.IndexOf(':');
            proxy_port = default_port;
            if (c < 0)
                // "<host>"
                proxy_host = ProtocolProxyServer;
            else
            {
                // "<host>:<port>"
                proxy_host = ProtocolProxyServer.Substring(0, c);
                Int32.TryParse(ProtocolProxyServer.Substring(c + 1),
                    out proxy_port);
            }
        }

        /// <summary>
        /// Replace the proxy for the HTTP protocol; proxy settings for
        /// the other protocols are left unchanged
        /// </summary>
        /// <remarks>
        /// ProxyEnable is not modified either, and must be updated separately.
        /// </remarks>
        public void SetHttpSpecificProxy(string proxy_host, int proxy_port)
        {
            var GlobalProxyServer = proxy_host + ":" + proxy_port;
            var HttpProxyServer = "http=" + GlobalProxyServer;

            if (ProxyServer == null)
            {
                ProxyServer = HttpProxyServer;
                return;
            }

            if (ProxyServer.IndexOf(';') > -1)
            {
                // Find and modify the http-only part
                var items = ProxyServer.Split(';');
                int i;
                for (i = 0; i < items.Length; i++)
                    if (items[i].StartsWith("http="))
                    {
                        items[i] = HttpProxyServer;
                        break;
                    }
                if (i == items.Length)
                    // We didn't find an entry with "http=", so we add it
                    ProxyServer = ProxyServer + ";" + HttpProxyServer;
                else
                    ProxyServer = String.Join(";", items);
                return;
            }

            if (ProxyServer.IndexOf('=') < 0)
            {
                if (ProxyServer.Equals(GlobalProxyServer))
                    // No change required (this avoids changing ProxyServer
                    // from, say, "localhost:2000" to
                    // "ftp=localhost:2000;http=localhost:2000;...")
                    return;

                // Previously, the same proxy was used for all protocols.
                // We need to introduce a distinction between the different
                // protocols so that we can use our proxy for HTTP-only.
                ProxyServer = "ftp=" + ProxyServer + ";" + HttpProxyServer +
                    ";https=" + ProxyServer + ";socks=" + ProxyServer;
                return;
            }

            if (ProxyServer.StartsWith("http="))
                // There's only a HTTP-only proxy defined, so we replace it
                ProxyServer = HttpProxyServer;
            else
                // There's a proxy defined for a protocol which isn't HTTP, so
                // we add our HTTP item.
                ProxyServer += ";" + HttpProxyServer;
        }

        /// <summary>
        /// Human-readable representation
        /// </summary>
        public override string ToString()
        {
            if (!ProxyEnable)
                return "no proxy";
            return ProxyServer +
                (ProxyOverride == null
                    ? ""
                    : " (bypass: " + ProxyOverride + ")");
        }
    }
}
