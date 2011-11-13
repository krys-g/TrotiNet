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
        /// Constructor
        /// </summary>
        public SystemProxySettings(bool proxyEnable,
            string proxyServer, string proxyOverride)
        {
            ProxyEnable = proxyEnable;
            ProxyServer = proxyServer;
            ProxyOverride = proxyOverride;
        }

        /// <summary>
        /// Retrieve the HTTP-specific proxy settings
        /// </summary>
        public void GetHttpOnlyProxy(out string proxy_host, out int proxy_port)
        {
            proxy_host = null;
            proxy_port = 0;

            if (!ProxyEnable)
                return;

            // Recall that ProxyServer can have one of these two forms:
            //   [http=]localhost:2000
            //   ftp=ip1:2002;http=ip2:2000;https=ip3:2001;socks=ip4:2003
            string HttpProxyServer = null;
            if (ProxyServer.IndexOf(';') > -1)
            {
                // Find the http-only part
                var items = ProxyServer.Split(';');
                for (int i = 0; i < items.Length; i++)
                    if (items[i].StartsWith("http="))
                    {
                        HttpProxyServer = items[i];
                        break;
                    }
                if (HttpProxyServer == null)
                    // We didn't find an entry with "http="
                    return;
            }
            else
                // Either "<host>[:<port>]", or "http=<host>[:<port>]"
                HttpProxyServer = ProxyServer;

            // Again, we have "<host>[:<port>]" or "http=<host>[:<port>]"
            if (HttpProxyServer.StartsWith("http="))
                HttpProxyServer = HttpProxyServer.Substring(5);

            // Now we only have "<host>[:<port>]"
            var c = HttpProxyServer.IndexOf(':');
            proxy_port = 80;
            if (c < 0)
                // "<host>"
                proxy_host = HttpProxyServer;
            else
            {
                // "<host>:<port>"
                proxy_host = HttpProxyServer.Substring(0, c);
                Int32.TryParse(HttpProxyServer.Substring(c + 1),
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
        public void SetHttpOnlyProxy(string proxy_host, int proxy_port)
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
