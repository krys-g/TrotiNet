namespace TrotiNet
{

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
        public ProxyDummyEcho(HttpSocket socketBP, bool PrintEchoPrefix) :
            base(socketBP)
        {
            bPrintEchoPrefix = PrintEchoPrefix;
        }

        /// <summary>
        /// Static constructor with <c>PrintEchoPrefix = true</c>
        /// </summary>
        static public AbstractProxyLogic CreateEchoProxy(HttpSocket socketBP)
        {
            return new ProxyDummyEcho(socketBP, true);
        }

        /// <summary>
        /// Static constructor with <c>PrintEchoPrefix = false</c>
        /// </summary>
        static public AbstractProxyLogic CreateMirrorProxy(HttpSocket socketBP)
        {
            return new ProxyDummyEcho(socketBP, false);
        }

        /// <summary>
        /// Dummy logic loop, for test purposes
        /// </summary>
        override public bool LogicLoop()
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
