/*
 * This file is part of the tutorial on how to use the TrotiNet library.
 *
 * Here, we show how to derive a transparent proxy from the base class
 * ProxyLogic which implements all the necessary logic related to the 
 * HTTP protocol.
 *
 * TransparentProxy is a proxy that does not change the semantics of
 * the communication, but simply logs requests and answers. The purpose
 * of this example is to show the two callbacks OnReceiveRequest()
 * and OnReceiveResponse(), which are called by the base class ProxyLogic.
 */
using System;

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
}