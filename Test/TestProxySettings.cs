using System;
using NUnit.Framework;

namespace TrotiNet.Test
{
    [TestFixture]
    class TestGetSetHttpProxySettings
    {
        const string host1 = "localhost";
        const string host2 = "extrahost";
        const int port1 = 1000;
        const int port2 = 2000;
        const string proxy1a = "localhost:1000";
        const string proxy1b = "http=localhost:1000";
        const string proxy1c = "https=localhost:1000";
        const string proxy2a = "extrahost:2000";
        const string proxy2b = "http=extrahost:2000";
        const string proxy2c = "ftp=localhost:1000;" + proxy2b +
            ";https=localhost:1000;socks=localhost:1000";
        const string proxy2d = "ftp=extrahost:2000;" + proxy1b +
            ";https=extrahost:2000;socks=extrahost:2000";
        const string proxy3b = "ftp=localhost:1000";
        const string proxy3c = "ftp=localhost:1000;" + proxy2b;

        [Test]
        public void TestHttpsSpecific()
        {
            var sps = new SystemProxySettings(true, proxy1a, null);
            string host;
            int port;
            sps.GetHttpsSpecificProxy(out host, out port);
            Assert.AreEqual(host1, host);
            Assert.AreEqual(port1, port);

            sps = new SystemProxySettings(true, proxy1b, null);
            sps.GetHttpsSpecificProxy(out host, out port);
            Assert.AreEqual(null, host);

            sps = new SystemProxySettings(true, proxy1c, null);
            sps.GetHttpsSpecificProxy(out host, out port);
            Assert.AreEqual(host1, host);
            Assert.AreEqual(port1, port);

            sps = new SystemProxySettings(true, proxy2d, null);
            sps.GetHttpsSpecificProxy(out host, out port);
            Assert.AreEqual(host2, host);
            Assert.AreEqual(port2, port);

            sps = new SystemProxySettings(true, proxy3c, null);
            sps.GetHttpsSpecificProxy(out host, out port);
            Assert.AreEqual(null, host);
        }

        [Test]
        public void TestProxySetButNoHost()
        {
            var sps = new SystemProxySettings(true, null, null);
            string host;
            int port;
            sps.GetHttpSpecificProxy(out host, out port);
            Assert.IsNull(host);
        }

        [Test]
        public void TestWhenNoPreviousProxy()
        {
            var sps = new SystemProxySettings(false, null, null);
            sps.SetHttpSpecificProxy(host1, port1);
            Assert.IsFalse(sps.ProxyEnable);
            sps.ProxyEnable = true;
            Assert.AreEqual(proxy1b, sps.ProxyServer);
            Assert.AreEqual(null, sps.ProxyOverride);

            string host;
            int port;
            sps.GetHttpSpecificProxy(out host, out port);
            Assert.AreEqual(host1, host);
            Assert.AreEqual(port1, port);

            sps = new SystemProxySettings(false, proxy1a, "blah");
            sps.SetHttpSpecificProxy(host2, port2);
            Assert.IsFalse(sps.ProxyEnable);
            sps.ProxyEnable = true;
            Assert.AreEqual(proxy2c, sps.ProxyServer);
            Assert.AreEqual("blah", sps.ProxyOverride);

            sps.GetHttpSpecificProxy(out host, out port);
            Assert.AreEqual(host2, host);
            Assert.AreEqual(port2, port);
        }

        [Test]
        public void TestWhenPreviousAllProtocolProxy()
        {
            var sps = new SystemProxySettings(true, proxy2a, null);
            sps.SetHttpSpecificProxy(host1, port1);
            Assert.AreEqual(proxy2d, sps.ProxyServer);
            Assert.AreEqual(null, sps.ProxyOverride);

            string host;
            int port;
            sps.GetHttpSpecificProxy(out host, out port);
            Assert.AreEqual(host1, host);
            Assert.AreEqual(port1, port);
        }

        [Test]
        public void TestWhenPreviousHtmlOnlyProxy()
        {
            var sps = new SystemProxySettings(true, proxy2b, null);
            sps.SetHttpSpecificProxy(host1, port1);
            Assert.AreEqual(proxy1b, sps.ProxyServer);
            Assert.AreEqual(null, sps.ProxyOverride);

            string host;
            int port;
            sps.GetHttpSpecificProxy(out host, out port);
            Assert.AreEqual(host1, host);
            Assert.AreEqual(port1, port);
        }

        [Test]
        public void TestWhenPreviousFtpOnlyProxy()
        {
            var sps = new SystemProxySettings(true, proxy3b, null);
            sps.SetHttpSpecificProxy(host2, port2);
            Assert.AreEqual(proxy3c, sps.ProxyServer);
            Assert.AreEqual(null, sps.ProxyOverride);

            string host;
            int port;
            sps.GetHttpSpecificProxy(out host, out port);
            Assert.AreEqual(host2, host);
            Assert.AreEqual(port2, port);
        }

        [Test]
        public void TestWhenNoActualChange()
        {
            var sps = new SystemProxySettings(true, proxy1a, null);
            Assert.AreEqual(proxy1a, sps.ProxyServer);
            sps.SetHttpSpecificProxy(host1, port1);
            Assert.AreEqual(proxy1a, sps.ProxyServer);

            sps = new SystemProxySettings(true, proxy1b, null);
            Assert.AreEqual(proxy1b, sps.ProxyServer);
            sps.SetHttpSpecificProxy(host1, port1);
            Assert.AreEqual(proxy1b, sps.ProxyServer);
        }
    }
}
