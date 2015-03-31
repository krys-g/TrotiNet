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

namespace TrotiNet.Example
{
    public static class Example
    {
        public static void Main()
        {
            Utils.Log_Init();

            const int port = 12345;
            const bool useIPv6 = false;

            var server = new TcpServer(port, useIPv6);

            server.Start(TransparentProxy.CreateProxy);
            //Server.Start(RedirectingProxy.CreateProxy);
            //Server.Start(RewritingProxy.CreateProxy);

            server.InitListenFinished.WaitOne();
            if (server.InitListenException != null)
                throw server.InitListenException;

            while (true)
                System.Threading.Thread.Sleep(1000);

            //Server.Stop();
        }
    }
}