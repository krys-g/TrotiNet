/*
 * This file serves as a tutorial on how to use the TrotiNet library.
 *
 * Three example proxies are given:
 *  - TransparentProxy, a minimal example that simply observes requests
 *  - RedirectingProxy, a proxy that modifies the target of some requests
 *  - RewritingProxy, a proxy that modifies the content of some resources
 *
 * This file is simply the outer layout that instantiates a proxy, and starts
 * it.
 */

using System;
using TrotiNet.Example;

namespace TrotiNet.Example
{
    public static class Example
    {
        public static void Main()
        {
            Utils.Log_Init();

            int port = 12345;
            bool bUseIPv6 = false;

            var Server = new TcpServer(port, bUseIPv6);

            //Server.Start(TransparentProxy.CreateProxy);
            //Server.Start(RedirectingProxy.CreateProxy);
            Server.Start(RewritingProxy.CreateProxy);

            Server.InitListenFinished.WaitOne();
            if (Server.InitListenException != null)
                throw Server.InitListenException;

            while (true)
                System.Threading.Thread.Sleep(1000);

            //Server.Stop();
        }
    }
}
