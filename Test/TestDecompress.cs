using System.Net.Sockets;
using System.Reflection;
using NUnit.Framework;

namespace TrotiNet.Test
{
    [TestFixture]
    class TestDecompress
    {
        class DummyProxy : ProxyLogic
        {
            public DummyProxy(string fakeEncoding) :
                base(new HttpSocket(new Socket(AddressFamily.InterNetwork,
                    SocketType.Dgram, ProtocolType.Udp)))
            {
                ResponseHeaders = new HttpHeaders();
                ResponseHeaders.ContentEncoding = fakeEncoding;
            }
        }

        [Test]
        public void TestUndoGzip()
        {
            // Test decompressing content that has been gzip'ed

            var asm = Assembly.GetExecutingAssembly();
            var inS = asm.GetManifestResourceStream(
                "TrotiNet.Test.Resources.raven.gzip");
            Assert.AreEqual(13833, inS.Length);
            var expS = asm.GetManifestResourceStream(
                "TrotiNet.Test.Resources.raven.html");
            Assert.AreEqual(38930, expS.Length);

            var dummy_proxy = new DummyProxy("gzip");
            var outS = dummy_proxy.GetResponseMessageStream(inS);

            int avail = (int) expS.Length;
            byte[] b1 = new byte[1024];
            byte[] b2 = new byte[1024];
            while (avail > 0)
            {
                int to_read = avail < 1024 ? avail : 1024;
                int r1 = outS.Read(b1, 0, to_read);
                Assert.AreEqual(to_read, r1);
                int r2 = expS.Read(b2, 0, to_read);
                avail -= r2;

                Assert.AreEqual(to_read, r2);
                for (int i = 0; i < 1024; i++)
                    Assert.AreEqual(b2[i], b1[i]);
            }
        }
    }
}
