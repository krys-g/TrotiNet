using System.Net.Sockets;
using NUnit.Framework;
using TrotiNet;

namespace TrotiNet.Test
{
    /// <summary>
    /// Boring wrapper for TcpServer
    /// </summary>
    internal class TcpServerTest : TcpServer
    {
        public const int port = 27389;

        public TcpServerTest(OnNewClient apl, bool bUseIPv6) :
            base(port, bUseIPv6)
        {
            Start(apl);
        }
    }

    /// <summary>
    /// Common utility code for TCP and HTTP tests
    /// </summary>
    internal class TcpCommon
    {
        static TcpServerTest server;
        static bool bUseIPv6;

        /// <summary>
        /// Set up a simple server that echoes everything it receives back to
        /// the sender
        /// </summary>
        static public void Setup(bool UseIPv6)
        {
            bUseIPv6 = UseIPv6;
            server = new TcpServerTest(ProxyDummyEcho.CreateMirrorProxy,
                bUseIPv6);
        }

        /// <summary>
        /// Stop the server started by Setup()
        /// </summary>
        static public void TearDown()
        {
            server.Stop();
        }

        /// <summary>
        /// Send a message through the TCP channel, and expect it to
        /// return unchanged
        /// </summary>
        /// <remarks>
        /// Useful for testing I/O levels 1 and 2.
        /// This method must be called between Setup() and TearDown().
        /// </remarks>
        static public void DoMsgRoundTrip(string msg_send, string msg_expect)
        {
            byte[] to_send = System.Text.Encoding.ASCII.GetBytes(msg_send);

            using (var hs = new HttpSocket(
                new EchoSocket(bUseIPv6, to_send).socket))
            {
                var msg_receive = hs.ReadAsciiLine();
                Assert.AreEqual(msg_expect, msg_receive);
                hs.CloseSocket();
            }
        }

        /// <summary>
        /// Parse a HTTP header after sending it through the TCP channel
        /// </summary>
        /// <returns>A parsed version of <param>header</param></returns>
        /// <remarks>
        /// This method must be called between Setup() and TearDown().
        /// </remarks>
        static public HttpHeaders ParseHttpHeaders(string header)
        {
            byte[] to_send = System.Text.Encoding.ASCII.GetBytes(header);

            var es = new EchoSocket(bUseIPv6, to_send);
            using (var hs = new HttpSocket(es.socket))
            {
                es.socket.Shutdown(SocketShutdown.Send);
                return new TrotiNet.HttpHeaders(hs);
            }
        }

        /// <summary>
        /// Parse a HTTP header after sending it through the TCP channel
        /// </summary>
        /// <returns>A parsed version of <param>rq_line</param></returns>
        /// <remarks>
        /// This method must be called between Setup() and TearDown().
        /// </remarks>
        static public HttpRequestLine ParseHttpRequestLine(string rq_line)
        {
            byte[] to_send = System.Text.Encoding.ASCII.GetBytes(rq_line);

            var es = new EchoSocket(bUseIPv6, to_send);
            using (var hs = new HttpSocket(es.socket))
            {
                es.socket.Shutdown(SocketShutdown.Send);
                return new HttpRequestLine(hs);
            }
        }

        /// <summary>
        /// Parse a HTTP status line after sending it through the TCP channel
        /// </summary>
        /// <returns>A parsed version of <param>status_line</param></returns>
        /// <remarks>
        /// This method must be called between Setup() and TearDown().
        /// </remarks>
        static public HttpStatusLine ParseHttpStatusLine(string status_line)
        {
            byte[] to_send = System.Text.Encoding.ASCII.GetBytes(status_line);

            var es = new EchoSocket(bUseIPv6, to_send);
            using (var hs = new HttpSocket(es.socket))
            {
                es.socket.Shutdown(SocketShutdown.Send);
                return new HttpStatusLine(hs);
            }
        }
    }
}
