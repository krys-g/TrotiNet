using System;
using System.Collections.Generic;
using System.Text;
using System.Net.Sockets;
using System.IO;
using System.Security;

namespace TrotiNet
{
    static class SocketEx
    {
        /// <summary>
        /// Read available data from socket to stream
        /// </summary>
        /// <returns>Input stream</returns>
        public static void ReadToStream(this Socket socket, MemoryStream stream)
        {
            int amount = socket.Available;

            while (stream.Length < amount)
            {
                byte[] data = new byte[amount - stream.Length];

                int readAmount = socket.Receive(data);

                stream.Write(data, 0, readAmount);
            }
        }

        /// <summary>
        /// Write data to socket
        /// </summary>
        /// <param name="data">Array of bytes with data to be sended</param>
        public static void Write(this Socket socket, byte[] data)
        {
            int sentAmount = 0;

            while (sentAmount < data.Length)
            {
                sentAmount += socket.Send(
                    data,
                    sentAmount,
                    data.Length - sentAmount,
                    SocketFlags.None);
            }
        }

        /// <summary>
        /// Write data from stream to socket
        /// </summary>
        /// <param name="data">Stream with data to be sended</param>
        public static void Write(this Socket socket, MemoryStream data)
        {
            socket.Write(data.ToArray());
        }
    }
}
