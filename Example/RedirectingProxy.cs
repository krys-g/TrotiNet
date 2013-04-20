/*
 * This file is part of the tutorial on how to use the TrotiNet library.
 *
 * Here, we show how to derive a proxy that redirects some requests as
 * they pass through it, and deny some others altogether.
 *
 * In this example, we divert the regular request pipeline by
 * setting the next pipeline step explicitly, via the State.NextStep
 * variable.
 */
using System;

namespace TrotiNet.Example
{
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
}
