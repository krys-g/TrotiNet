// Uncomment to debug I/O level 1
//#define DEBUG_IO_1

using System;
using System.Net.Sockets;
using System.Text;

namespace TrotiNet
{
    /// <summary>
    /// Communication state between two hosts
    /// </summary>
    public class HttpSocket: IDisposable
    {
#if DEBUG_IO_1
        static readonly log4net.ILog log = Log.Get();
#endif

        /// <summary>
        /// Socket UID.
        /// </summary>
        public int id;


        /// <summary>
        /// Set the TCP Keep Alive option on the socket
        /// </summary>
        public bool KeepAlive
        {
            get { return _KeepAlive; }
            set
            {
                if (_KeepAlive != value)
                {
                    LowLevelSocket.SetSocketOption(SocketOptionLevel.Socket,
                        SocketOptionName.KeepAlive, value);
                    _KeepAlive = value;
                }
            }
        }
        bool _KeepAlive;

        /// <summary>
        /// Wrap a Socket instance into a HttpSocket instance
        /// </summary>
        public HttpSocket(Socket socket)
        {
            LowLevelSocket = socket;

            Buffer = new byte[BufferSize];
            sb = new StringBuilder(128);
        }

        /// <summary>
        /// Close the wrapped socket
        /// </summary>
        public void Dispose()
        {
            if (LowLevelSocket != null)
            {
                LowLevelSocket.Close();
                    // Note: Socket.Close() just calls Socket.Dispose()
                LowLevelSocket = null;
            }
        }

#region I/O level 1: plain C# socket interface
        /// <summary>
        /// Returns the wrapped socket
        /// </summary>
        protected Socket LowLevelSocket = null;

#if DEBUG_IO_1
        void Trace(string msg)
        {
            log.Debug("[S" + id + "] " + msg);
        }
#endif

        /// <summary>
        /// Close the internal socket
        /// </summary>
        public void CloseSocket()
        {
            if (LowLevelSocket == null)
                return;
            try { LowLevelSocket.Shutdown(SocketShutdown.Both); }
            catch { /* ignore */ }
            try { LowLevelSocket.Close(); }
            catch { /* ignore */ }
            LowLevelSocket = null;
        }

        /// <summary>
        /// Returns true if the socket has been closed, or has become
        /// unresponsive
        /// </summary>
        public bool IsSocketDead(/*bool bTestSend*/)
        {
            if (LowLevelSocket == null)
                return true;
            if (!LowLevelSocket.Connected)
                return true;

            // XXX NOT TESTED
/*
            // Trick: one way to see if a socket is still valid is
            // to try and write zero byte to it.
            if (bTestSend)
            try
            {
                int save = LowLevelSocket.SendTimeout;
                LowLevelSocket.SendTimeout = 1;
                LowLevelSocket.Send(Buffer, 0, SocketFlags.None);
                LowLevelSocket.SendTimeout = save;
            }
            catch { return true; }
 */
            return false;
        }
#endregion

#region I/O level 2: Buffered line-based and raw I/O

        uint BufferPosition;

        const uint BufferSize = 8192;
            // 8192 seems to be the default buffer size for the Socket object

        /// <summary>
        /// How many bytes of data are available in the receive buffer
        /// (starting at offset 0)
        /// </summary>
        protected uint AvailableData { get; private set; }

        /// <summary>
        /// Receive buffer
        /// </summary>
        public byte[] Buffer { get; protected set; }

        /// <summary>
        /// True if ReadAsciiLine may have loaded bytes in the buffer
        /// that ReadRaw should use
        /// </summary>
        bool UseLeftOverBytes;

        StringBuilder sb;

        /// <summary>
        /// Reads a LF-delimited (or CRLF-delimited) line from the socket,
        /// and returns it (without the trailing newline character)
        /// </summary>
        /// Content is expected to be in ASCII 8-bit (UTF-8 also works).
        public string ReadAsciiLine()
        {
            sb.Length = 0;
            bool bHadCR = false;
            while (true)
            {
                if (AvailableData == 0)
                {
                    if (ReadRaw() == 0)
                        // Connection closed while we were waiting for data
                        throw new IoBroken();
                    UseLeftOverBytes = true;
                }

                // Newlines in HTTP headers are expected to be CRLF.
                // However, for better robustness, RFC 2616 recommends
                // ignoring CR, and considering LF as new lines (section 19.3)
                byte b = Buffer[BufferPosition++];
                AvailableData--;

                if (b == '\n')
                    break;
                if (bHadCR)
                    sb.Append('\r');
                if (b == '\r')
                    bHadCR = true;
                else
                {
                    bHadCR = false;
                    char c = (char)b;
                    sb.Append(c);
                }
            }
            return sb.ToString();
        }

        /// <summary>
        /// Read buffered binary data
        /// </summary>
        /// <remarks>
        /// A read operation (for instance, ReadAsciiLine) may have loaded
        /// the buffer with some data which ended up not being used.
        /// If that's the case, then ReadBinary uses it (ReadRaw does not).
        /// </remarks>
        public uint ReadBinary()
        {
            bool bLeftOver = UseLeftOverBytes;
            UseLeftOverBytes = false;
            if (bLeftOver && AvailableData > 0)
            {
                if (BufferPosition != 0)
                    // Move the unread bytes at the beginning of the buffer.
                    // Note that Array.Copy knows how to handle overlaps
                    // correctly, i.e. it's a memmove, not a memcpy.
                    Array.Copy(Buffer, BufferPosition, Buffer, 0,
                        AvailableData);

                uint r = AvailableData;
                AvailableData = 0;
                BufferPosition = 0;
                return r;
            }
            return ReadRaw();
        }

        /// <summary>
        /// Read a block of data from the socket; unread data that was in the
        /// buffer is dropped
        /// </summary>
        /// <remarks>
        /// BufferPosition is reset. If there were unread data in the buffer,
        /// it's lost.
        /// </remarks>
        protected uint ReadRaw()
        {
#if DEBUG_IO_1
            Trace("ReadRaw before Receive " + LowLevelSocket.Connected);
#endif

            int r = LowLevelSocket.Receive(Buffer);
            // Notes:
            // - if we are using non-infinite timeouts (not true from
            //  TrotiNet.Test), timeouts would be signalled by thrown
            //  SocketException's.
            // - if we were using non-blocking sockets with no data
            //  available, then again a SocketException would be thrown.
            // So if Receive() returns 0, it really means the connection
            // has been closed.

#if DEBUG_IO_1
            if (r == 0)
                Trace("ReadRaw: connection closed (" +
                    LowLevelSocket.Connected + ")");
            else
                Trace("ReadRaw after Receive got " + r + " bytes");
#endif

            System.Diagnostics.Debug.Assert(r >= 0);
                // Note: r = 0 means connection closed

            AvailableData = (uint)r;
            BufferPosition = 0;
            return AvailableData;
        }

        /// <summary>
        /// Transfer data from this socket to the destination socket
        /// </summary>
        /// <returns>The number of bytes sent</returns>
        public uint TunnelDataTo(HttpSocket dest)
        {
            uint total_sent = 0;
            if (AvailableData == 0)
                ReadRaw();
            while(AvailableData > 0)
            {
                uint sent = dest.WriteRaw(Buffer, BufferPosition,
                    AvailableData);
                if (sent < AvailableData)
                    throw new IoBroken();
                total_sent += sent;
                ReadRaw();
            }
            return total_sent;
        }

        /// <summary>
        /// Read <code>nb_bytes</code> bytes from the socket,
        /// and send it to the destination socket
        /// </summary>
        /// <returns>The number of bytes sent</returns>
        public uint TunnelDataTo(HttpSocket dest, uint nb_bytes)
        {
            uint total_sent = 0;
            while (nb_bytes > 0)
            {
                if (AvailableData == 0)
                    if (ReadRaw() == 0)
                        throw new IoBroken();
                uint to_send = AvailableData;
                if (to_send > nb_bytes)
                {
                    UseLeftOverBytes = true;
                    to_send = nb_bytes;
                }
                uint sent = dest.WriteRaw(Buffer, BufferPosition, to_send);
                if (sent < to_send)
                    throw new IoBroken();
                total_sent += sent;
                nb_bytes -= sent;
                AvailableData -= sent;
                BufferPosition += sent;
            }
            return total_sent;
        }

        static readonly byte[] CRLF_b = { 0x0d, 0x0a };

        /// <summary>
        /// Write an ASCII string, a CR character, and a LF character to the
        /// socket
        /// </summary>
        public uint WriteAsciiLine(string s)
        {
            uint r = WriteBinary(System.Text.Encoding.ASCII.GetBytes(s));
            r += WriteBinary(CRLF_b);
            return r;
        }

        /// <summary>
        /// Write an array of bytes to the socket
        /// </summary>
        public uint WriteBinary(byte[] b)
        {
            return WriteRaw(b, 0, (uint)b.Length);
        }

        /// <summary>
        /// Write the first <code>nb_bytes</code> of <code>b</code> to the
        /// socket
        /// </summary>
        public uint WriteBinary(byte[] b, uint nb_bytes)
        {
            return WriteRaw(b, 0, nb_bytes);
        }

        /// <summary>
        /// Write <code>nb_bytes</code> of <code>b</code>, starting at offset
        /// <code>offset</code> to the socket
        /// </summary>
        protected uint WriteRaw(byte[] b, uint offset, uint nb_bytes)
        {
            int r = LowLevelSocket.Send(b, (int)offset, (int)nb_bytes,
                SocketFlags.None);
            if (r < 0)
                r = 0;
            return (uint)r;
        }
#endregion

#region I/O level 3: HTTP-based I/O

        void SendHttpError(string ErrorCodeAndReason)
        {
            string html_body = "<html>\n <body>\n  <h1>" +
                ErrorCodeAndReason + "</h1>\n </body>\n</html>";
            WriteBinary(System.Text.Encoding.ASCII.GetBytes(
                "HTTP/1.0 " + ErrorCodeAndReason + "\r\n" +
                "Connection: close\r\n" +
                "Content-Length: " + html_body.Length + "\r\n" +
                "\r\n" + html_body + "\r\n"));
        }

        /// <summary>
        /// Send a HTTP 400 error over the socket
        /// </summary>
        public void Send400()
        {
            SendHttpError("400 Bad Request");
        }

        /// <summary>
        /// Send a HTTP 404 error over the socket
        /// </summary>
        public void Send404()
        {
            SendHttpError("404 Not Found");
        }

        /// <summary>
        /// Send a HTTP 501 error over the socket
        /// </summary>
        public void Send501()
        {
            SendHttpError("501 Not Implemented");
        }

        /// <summary>
        /// Tunnel a HTTP-chunked blob of data
        /// </summary>
        /// <remarks>
        /// The tunneling stops when the last chunk, identified by a
        /// size of 0, arrives. The optional trailing entities are also
        /// transmitted (but otherwise ignored).
        /// </remarks>
        public void TunnelChunkedDataTo(HttpSocket dest)
        {
            // (RFC 2616, sections 3.6.1, 19.4.6)
            while (true)
            {
                string chunk_header = ReadAsciiLine();
                if (chunk_header.Length == 0)
                    throw new HttpProtocolBroken(
                        "Expected chunk header missing");
                int sc = chunk_header.IndexOf(';');
                string hexa_size;
                if (sc > -1)
                    // We have chunk extensions: ignore them
                    hexa_size = chunk_header.Substring(0, sc);
                else
                    hexa_size = chunk_header;
                uint size;
                try
                {
                    size = Convert.ToUInt32(hexa_size, 16);
                }
                catch
                {
                    string s = chunk_header.Length > 20
                        ? (chunk_header.Substring(0, 17) + "...")
                        : chunk_header;
                    throw new HttpProtocolBroken(
                        "Could not parse chunk size in: " + s);
                }

                dest.WriteAsciiLine(chunk_header);
                if (size == 0)
                    break;
                TunnelDataTo(dest, size);
                // Read one more CRLF
                chunk_header = ReadAsciiLine();
                System.Diagnostics.Debug.Assert(chunk_header.Length == 0);
            }
            string line;
            do
            {
                // Tunnel any trailing entity headers
                line = ReadAsciiLine();
                dest.WriteAsciiLine(line);
            } while (line.Length != 0);
        }

#endregion
    }
}
