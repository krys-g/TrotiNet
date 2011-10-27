using System;
using NUnit.Framework;

namespace TrotiNet.Test
{
    [TestFixture]
    class TestHeaders
    {
        [Test]
        public static void TestHeadersInOrder()
        {
            string[] c1 = { "E2E4" };
            string h_c1 = "Connection: E2E4\r\n";
            string[] c2 = { "E7E5" };
            string h_c2 = "Connection: E7E5\r\n";
            string host = "chez.moi";
            string h_host = "Host: " + host + "\r\n";
            int ContentLength = 1951;
            string h_cl = "Content-Length: 1951\r\n";

            var hh = new HttpHeaders();
            hh.Connection = c1;
            Assert.AreEqual(h_c1, hh.HeadersInOrder);
            hh.Host = host;
            Assert.AreEqual(h_c1 + h_host, hh.HeadersInOrder);
            hh.Connection = c2;
            Assert.AreEqual(h_c2 + h_host, hh.HeadersInOrder);
            hh.Connection = null;
            Assert.AreEqual(h_host, hh.HeadersInOrder);
            hh.ContentLength = (uint?) ContentLength;
            Assert.AreEqual(h_host + h_cl, hh.HeadersInOrder);
            hh.ContentLength = null;
            Assert.AreEqual(h_host, hh.HeadersInOrder);
            hh.Host = null;
            Assert.IsTrue(String.IsNullOrEmpty(hh.HeadersInOrder));
        }
    }
}
