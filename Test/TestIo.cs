using System;
using NUnit.Framework;

namespace TrotiNet.Test
{
    [TestFixture]
    partial class TestTCPv4
    {
        [TestFixtureSetUp]
        static public void FixtureSetUp()
        {
            TcpCommon.Setup(false);
        }

        [TestFixtureTearDown]
        static public void FixtureTearDown()
        {
            TcpCommon.TearDown();
        }

        [Test]
        [ExpectedException(typeof(IoBroken))]
        static public void TestBrokenHttpHeaders1()
        {
            // Missing empty line
            var incomplete = "Host:www.crunchy.frog\r\n";
            HttpHeaders hh = TcpCommon.ParseHttpHeaders(incomplete);
        }

        [Test]
        [ExpectedException(typeof(HttpProtocolBroken))]
        static public void TestBrokenHttpHeaders2()
        {
            // Not a proper key:value entry
            var incomplete = "Buzz Lightyear\r\n\r\n";
            HttpHeaders hh = TcpCommon.ParseHttpHeaders(incomplete);
        }

        [Test]
        static public void TestMessageRoundTripIPv4()
        {
            var msg1 = "Sally sells seashells by the seashore.";
            TcpCommon.DoMsgRoundTrip(msg1 + "\n", msg1);
            TcpCommon.DoMsgRoundTrip(msg1 + "\r\n", msg1);

            var msg2a = "The seashells she sells are seashore shells,";
            var msg2b = "\r\nOf that I'm sure.\r\n";
            TcpCommon.DoMsgRoundTrip(msg2a + msg2b, msg2a);

            TcpCommon.DoMsgRoundTrip("\n", "");
            TcpCommon.DoMsgRoundTrip("\r\n", "");
        }

        [Test]
        static public void TestParseHttpHeaders()
        {
            // Testing standard parse, with random spaces
            var h1 =
                "Host:www.crunchy.frog\r\n" +
                "content-length     : 1242\r\n" +
                "Connection  : palom\r\n" +
                "cONNECTION  : porom\r\n\r\n";
            HttpHeaders hh1 = TcpCommon.ParseHttpHeaders(h1);
            Assert.AreEqual("www.crunchy.frog", hh1.Host);
            Assert.AreEqual(1242, hh1.ContentLength);
            Assert.AreEqual(2, hh1.Connection.Length);
            Array.Sort(hh1.Connection);
            Assert.AreEqual("palom", hh1.Connection[0]);
            Assert.AreEqual("porom", hh1.Connection[1]);

            // Testing LF instead of CRLF (which is wrong but
            // should still be accepted); testing null int
            var h2 =
                "Connection    :Porom\n" +
                "Host          :www.parrot.sketch\n" +
                "cONNECTION    :Palom\n\n";
            HttpHeaders hh2 = TcpCommon.ParseHttpHeaders(h2);
            Assert.AreEqual("www.parrot.sketch", hh2.Host);
            Assert.AreEqual(2, hh2.Connection.Length);
            Assert.IsNull(hh2.ContentLength);
            Array.Sort(hh2.Connection);
            Assert.AreEqual("palom", hh2.Connection[0]);
            Assert.AreEqual("porom", hh2.Connection[1]);
        }

        [Test]
        static public void TestParseHttpRequestLine()
        {
            // Test a well-formatted HTTP/1.1 request line
            var rq1 = "GET / HTTP/1.1";
            var rq1_nl = rq1 + "\r\n";
            HttpRequestLine hrl1 = TcpCommon.ParseHttpRequestLine(rq1_nl);
            Assert.AreEqual(rq1, hrl1.RequestLine);
            Assert.AreEqual("GET", hrl1.Method);
            Assert.AreEqual("/", hrl1.URI);
            Assert.AreEqual("1.1", hrl1.ProtocolVersion);

            // Test a well-formatted HTTP/1.0 request line
            var rq2 = "HEAD http://www.idm.fr/index.html HTTP/1.0\r\n";
            HttpRequestLine hrl2 = TcpCommon.ParseHttpRequestLine(rq2);
            Assert.AreEqual("HEAD", hrl2.Method);
            Assert.AreEqual("http://www.idm.fr/index.html", hrl2.URI);
            Assert.AreEqual("1.0", hrl2.ProtocolVersion);

            // Test a wonky-but-still-acceptable request line
            var rq3 = "OPTIONS   /freezepop    HTTP/1.2  \n";
            HttpRequestLine hrl3 = TcpCommon.ParseHttpRequestLine(rq3);
            Assert.AreEqual("OPTIONS", hrl3.Method);
            Assert.AreEqual("/freezepop", hrl3.URI);
            Assert.AreEqual("1.2", hrl3.ProtocolVersion);
        }

        [Test]
        static public void TestParseHttpStatusLine()
        {
            // Test a well-formatted status line
            var sl = "HTTP/1.0 200 OK";
            var sl_nl = sl + "\r\n";
            HttpStatusLine hsl = TcpCommon.ParseHttpStatusLine(sl_nl);
            Assert.AreEqual("1.0", hsl.ProtocolVersion);
            Assert.AreEqual(sl, hsl.StatusLine);
            Assert.AreEqual(200, hsl.StatusCode);
        }

        [Test]
        static public void TestHttpResponseBinary()
        {
            // Test the mixing of line-based and raw-based reads
            var msg = "123";
            var http_msg = "HTTP/1.0 200 OK\r\n\r\n" + msg;

            using (var hs = new HttpSocket(
                new EchoSocket(false, http_msg).socket))
            {
                var hsl = new HttpStatusLine(hs);
                Assert.AreEqual(200, hsl.StatusCode);
                var hh = new HttpHeaders(hs);
                uint r = hs.ReadBinary();
                Assert.AreEqual(r, 3);
                Assert.AreEqual('1', hs.Buffer[0]);
                Assert.AreEqual('2', hs.Buffer[1]);
                Assert.AreEqual('3', hs.Buffer[2]);
            }
        }

        [Test]
        static public void TestHttpResponseLine()
        {
            // Test the mixing of structured and unstructured
            // line-based reads
            var msg = "Captain Copyright";
            var http_msg = "HTTP/1.0 200 OK\r\n\r\n" + msg + "\r\n";

            using (var hs = new HttpSocket(
                new EchoSocket(false, http_msg).socket))
            {
                var hsl = new HttpStatusLine(hs);
                Assert.AreEqual(200, hsl.StatusCode);
                var hh = new HttpHeaders(hs);
                var line = hs.ReadAsciiLine();
                Assert.AreEqual(msg, line);
            }
        }

        [Test]
        static public void TestModifyHeader()
        {
            var vpc = "French";
            var vh = "your.host.for.tonight";
            var vc = "Canadian";
            var msg =
                "Proxy-Connection: " + vpc + "\r\n" +
                "Host: " + vh + "\r\n" +
                "Connection: " + vc + "\r\n\r\n";
            using (var hs = new HttpSocket(
                new EchoSocket(false, msg).socket))
            {
                var hr = new HttpHeaders(hs);
                Assert.AreEqual(1, hr.Connection.Length);
                Assert.AreEqual(1, hr.ProxyConnection.Length);
                Assert.AreEqual(vpc.ToLower(), hr.ProxyConnection[0]);
                hr.ProxyConnection = new string[] { vc };
                Assert.AreEqual(1, hr.ProxyConnection.Length);
                Assert.AreEqual(vc, hr.ProxyConnection[0]);
                Assert.AreEqual(vh.ToLower(), hr.Host);
                Assert.AreEqual(1, hr.Connection.Length);
                Assert.AreEqual(vc.ToLower(), hr.Connection[0]);
                hr.ProxyConnection = null;
                Assert.IsNull(hr.ProxyConnection);
                Assert.AreEqual(vh.ToLower(), hr.Host);
                Assert.AreEqual(1, hr.Connection.Length);
                Assert.AreEqual(vc.ToLower(), hr.Connection[0]);
            }
        }

        [Test]
        static public void TestTunnelData()
        {
            // Test binary tunneling
            byte[] data = { 1, 2, 3 };
            using (var hs = new HttpSocket(
                new EchoSocket(false, data).socket))
            {
                // We tunnel data... to ourselves!
                hs.TunnelDataTo(hs, 3);
                uint r = hs.ReadBinary();
                Assert.AreEqual(3, r);
                Assert.AreEqual(1, hs.Buffer[0]);
                Assert.AreEqual(2, hs.Buffer[1]);
                Assert.AreEqual(3, hs.Buffer[2]);
            }

            // Test the mixing of line-based and raw-based tunneling
            byte[] data2 = { (byte)'A', 13, 10, 42, 42, (byte)'B', 13, 10 };
            using (var hs = new HttpSocket(
                new EchoSocket(false, data2).socket))
            {
                // Ditto
                hs.TunnelDataTo(hs, (uint)data2.Length);
                string msgA = hs.ReadAsciiLine();
                Assert.AreEqual("A", msgA);
                hs.TunnelDataTo(hs, 2);
                string msgB = hs.ReadAsciiLine();
                Assert.AreEqual("B", msgB);
            }
        }
    }

    [TestFixture]
    class TestTCPv6
    {
        [TestFixtureSetUp]
        static public void FixtureSetUp()
        {
            TcpCommon.Setup(true);
        }

        [TestFixtureTearDown]
        static public void FixtureTearDown()
        {
            TcpCommon.TearDown();
        }

        [Test]
        static public void TestConnectionIPv6()
        {
            TcpCommon.DoMsgRoundTrip("meow\n", "meow");
        }
    }
}
