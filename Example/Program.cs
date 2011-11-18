/*
 * This file serves as a tutorial on how to use the TrotiNet library.
 *
 * Two example proxies are given. Both derive from the base class
 * ProxyLogic which implements all the necessary logic related to the HTTP
 * protocol.
 *
 * - TransparentProxy is a proxy that does not change the semantics of
 *   the communication, but simply logs requests and answers. The purpose
 *   of this example is to show the two callbacks OnReceiveRequest()
 *   and OnReceiveResponse(), which are called by the base class ProxyLogic.
 *
 * - RedirectingProxy shows how some requests may be redirected as they pass
 *   through the proxy, and some others may be denied altogether.
 *   In this second example, we divert the regular request pipeline by
 *   setting the next pipeline step explicitly.
 */

using System;
using TrotiNet.Example;

namespace TrotiNet.Example
{
    public class TransparentProxy : ProxyLogic
    {
        public TransparentProxy(HttpSocket clientSocket)
            : base(clientSocket) { }

        static new public TransparentProxy CreateProxy(HttpSocket clientSocket)
        {
            return new TransparentProxy(clientSocket);
        }

        protected override void OnReceiveRequest()
        {
            Console.WriteLine("-> " + RequestLine + " from HTTP referer " +
                RequestHeaders.Referer);
        }

        protected override void OnReceiveResponse()
        {
            Console.WriteLine("<- " + ResponseStatusLine +
                " with HTTP Content-Length: " +
                (ResponseHeaders.ContentLength ?? 0));
        }
    }

    public class RedirectingProxy : ProxyLogic
    {
        public RedirectingProxy(HttpSocket clientSocket)
            : base(clientSocket) { }

        static new public RedirectingProxy CreateProxy(HttpSocket clientSocket)
        {
            return new RedirectingProxy(clientSocket);
        }

        protected override void OnReceiveRequest()
        {
            // Make your thesis more interesting by upgrading
            // your primary source of information
            int i = RequestLine.URI.IndexOf("wikipedia.org");
            if (i > -1)
            {
                ChangeRequestURI("http://uncyclopedia.org/" +
                    RequestLine.URI.Substring(i + 14));
                return;
            }

            // Make yourself more productive
            if (RequestLine.URI.Contains("facebook.com"))
            {
                SocketBP.Send403();
                State.NextStep = AbortRequest;
            }
        }
    }

    public static class Example
    {
        public static void Main()
        {
            Utils.Log_Init();

            int port = 12345;
            bool bUseIPv6 = false;

            var Server = new TcpServer(port, bUseIPv6);

            //Server.Start(TransparentProxy.CreateProxy);
            Server.Start(RedirectingProxy.CreateProxy);

            Server.InitListenFinished.WaitOne();
            if (Server.InitListenException != null)
                throw Server.InitListenException;

            while (true)
                System.Threading.Thread.Sleep(1000);

            //Server.Stop();
        }
    }
}
