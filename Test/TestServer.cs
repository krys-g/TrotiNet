using System;
using System.Net.Sockets;
using NUnit.Framework;

namespace TrotiNet.Test
{
    [TestFixture]
    static class TestServer
    {
        const string ss = "SolidSnake";

        static AbstractProxyLogic OnConnectionTalk(HttpSocket hh)
        {
            hh.WriteAsciiLine(ss);
            return null;
        }

        static AbstractProxyLogic OnConnectionFail(HttpSocket hh)
        {
            // Should never be called
            Assert.Fail();
            return null;
        }

        [Test]
        public static void TestPortAlreadyAssigned()
        {
            int port = 20000;
            bool bUseIPv6 = false;
            TcpServer s1 = new TcpServer(port, bUseIPv6);
            TcpServer s2 = new TcpServer(port, bUseIPv6);

            s1.Start(OnConnectionTalk);
            s1.InitListenFinished.WaitOne();
            if (s1.InitListenException != null)
                throw s1.InitListenException;
            Assert.IsTrue(s1.IsListening);

            // Starting a second server on the same port should fail
            Console.WriteLine("Note: SocketException expected");
            s2.Start(OnConnectionFail);
            s2.InitListenFinished.WaitOne();
            Assert.IsNotNull(s2.InitListenException);
            var e = s2.InitListenException as SocketException;
            Assert.IsNotNull(e);
            Assert.AreEqual(10048, e.ErrorCode);
                // 10048 means "Only one usage of each socket address ..."
            Assert.IsFalse(s2.IsListening);

            // Check that the first server still works
            using (var hs = new HttpSocket(
                new EchoSocket(port, bUseIPv6).socket))
            {
                string s = hs.ReadAsciiLine();
                Assert.AreEqual(ss, s);
            }

            Assert.IsTrue(s1.IsListening);
            s1.Stop(); // stop the server in case we want to re-run the test
            Assert.IsFalse(s1.IsListening); // while we are here...
        }
    }
}
