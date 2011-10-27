using System;
using System.Net;
using System.Net.Sockets;
using TrotiNet;

namespace TrotiNet.Test
{
    internal class EchoSocket : IDisposable
    {
        public Socket socket;

        public EchoSocket(bool UseIPv6) : this(TcpServerTest.port, UseIPv6) { }

        public EchoSocket(int Port, bool UseIPv6)
        {
            socket = new Socket(UseIPv6
                ? AddressFamily.InterNetworkV6
                : AddressFamily.InterNetwork,
                SocketType.Stream, ProtocolType.Tcp);
            socket.SendTimeout = 10000;
            socket.ReceiveTimeout = 10000;
            socket.Connect(UseIPv6 ? "::1" : "127.0.0.1", Port);
        }

        public EchoSocket(bool UseIPv6, byte[] msg) : this(UseIPv6)
        {
            int sent = socket.Send(msg);
            if (sent != msg.Length)
                throw new Exception("EchoSocket sent " + sent + " bytes only");
        }

        public EchoSocket(bool UseIPv6, string msg) :
            this(UseIPv6, System.Text.Encoding.ASCII.GetBytes(msg))
        {
        }

        public void Dispose()
        {
            if (socket != null)
            {
                socket.Close();
                socket = null;
            }
        }
    }
}
