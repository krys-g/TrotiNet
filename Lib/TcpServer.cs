/*
    Generic TCP/IP server, initially inspired from:
    http://msdn.microsoft.com/en-us/library/fx6588te%28vs.71%29.aspx

    However, the MSDN example fails if you place a long Sleep() in
    AcceptCallback(), then try to open two client connections during the
    Sleep(). In that case, it appears that the second call to BeginAccept()
    executes in the same thread than its caller (StartListening), so no
    additional connection will be accepted until the last AcceptCallback()
    finishes. See the workaround in AcceptCallback().
*/

// Uncomment to debug BeginAccept/EndAccept
//#define DEBUG_ACCEPT_CONNECTION

using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Threading;
using log4net;

[assembly: InternalsVisibleTo("TrotiNet.Test")]
namespace TrotiNet
{
    /// <summary>
    /// Implementation of a TCP/IP server
    /// </summary>
    public class TcpServer
    {
        static readonly ILog log = Log.Get();

#if DEBUG_ACCEPT_CONNECTION
        void Pause()
        {
            log.Debug("Pause start");
            System.Threading.Thread.Sleep(3000);
            log.Debug("Pause end");
        }
#endif

        /// <summary>
        /// If not null, specify which address the listening socket should
        /// be bound to. If null, it will default to the loopback address.
        /// </summary>
        public IPAddress BindAddress { get; set; }

        /// <summary>
        /// Timer that calls CheckSockets regularly
        /// </summary>
        Timer CleanTimer;

        /// <summary>
        /// Set of open sockets, indexed by socket identifier
        /// </summary>
        protected Dictionary<int, HttpSocket> ConnectedSockets;

        /// <summary>
        /// Set if an error has occured while the server was initializing
        /// the listening thread
        /// </summary>
        public Exception InitListenException { get; protected set; }

        /// <summary>
        /// Set when the listening thread has finished its initialization
        /// (either successfully, or an exception has been thrown)
        /// </summary>
        /// <seealso cref="InitListenException"/>
        /// <seealso cref="IsListening"/>
        public ManualResetEvent InitListenFinished { get; protected set; }

        /// <summary>
        /// Set to true if the listening thread is currently listening
        /// for incoming connections
        /// </summary>
        public bool IsListening { get; protected set; }

        /// <summary>
        /// Set to true if the server is about to shut down
        /// </summary>
        protected bool IsShuttingDown { get; private set; }

        /// <summary>
        /// Incremented at every client connection
        /// </summary>
        int LastClientId;

        Thread ListeningThread;
        ManualResetEvent ListenThreadSwitch;

        /// <summary>
        /// Port used for local browser-proxy communication
        /// </summary>
        protected int LocalPort;

        /// <summary>
        /// Called every time a connection is accepted from the browser
        /// by the proxy. Must return the instance that will handle the
        /// communication for the new connection.
        /// </summary>
        public delegate AbstractProxyLogic OnNewClient(HttpSocket ss);

        OnNewClient OnClientStart;

        bool UseIPv6;

        /// <summary>
        /// Initialize, but do not start, a multi-threaded TCP server
        /// listening for localhost connections only
        /// </summary>
        /// <param name="localPort">TCP port to listen to</param>
        /// <param name="bUseIPv6">
        /// If true, listen on ::1 only. If false, listen on 127.0.0.1 only.
        /// </param>
        public TcpServer(int localPort, bool bUseIPv6)
        {
            if (localPort < 1)
                throw new ArgumentException("localPort");

            LocalPort = localPort;
            UseIPv6 = bUseIPv6;

            ConnectedSockets = new Dictionary<int, HttpSocket>();
            InitListenFinished = new ManualResetEvent(false);
            ListenThreadSwitch = new ManualResetEvent(false);
            ListeningThread = null;
        }

        /// <summary>
        /// Callback method for accepting new connections
        /// </summary>
        void AcceptCallback(IAsyncResult ar)
        {
            if (IsShuttingDown)
                return;

            // Have we really changed thread?
            if (ListeningThread.ManagedThreadId ==
                System.Threading.Thread.CurrentThread.ManagedThreadId)
            {
                // No! Give me a new thread!
                new Thread(() => AcceptCallback(ar)).Start();
                return;
            }

            // Get the socket that handles the client request
            Socket listener = (Socket)ar.AsyncState;
            Socket handler = listener.EndAccept(ar);

            // Signal the main thread to continue
            ListenThreadSwitch.Set();

#if DEBUG_ACCEPT_CONNECTION
            log.Debug("\tAcceptCallback sent signal");
#endif

            // Create the state object
            HttpSocket state = new HttpSocket(handler);
            state.id = ++LastClientId;

            lock (ConnectedSockets)
                ConnectedSockets[state.id] = state;

            AbstractProxyLogic proxy = null;
            try
            {
                proxy = OnClientStart(state);
            } catch (Exception e) { log.Error(e); }
            if (proxy == null)
            {
                CloseSocket(state);
                return;
            }

            // No need for asynchronous I/O from now on
            try
            {
                while (proxy.LogicLoop())
                    if (IsShuttingDown || state.IsSocketDead())
                        break;

                log.Debug("Shutting down socket");
            }
            catch (System.Net.Sockets.SocketException) { /* ignore */ }
            catch (TrotiNet.IoBroken) { /* ignore */ }
            catch (Exception e)
            {
                log.Error(e);
                log.Debug("Closing socket on error");
            }

            CloseSocket(state);
        }

        /// <summary>
        /// Close broken sockets
        /// </summary>
        /// <remarks>
        /// This function is called regularly to clean up the list of
        /// connected sockets.
        /// </remarks>
        void CheckSockets(object eventState)
        {
            try
            {
                lock (ConnectedSockets)
                {
                    foreach (var kv in ConnectedSockets)
                    {
                        try
                        {
                            int id = kv.Key;
                            HttpSocket state = kv.Value;
                            if (state == null || state.IsSocketDead())
                                ConnectedSockets.Remove(id);
                        }
                        catch (Exception e)
                        {
                            log.Error(e);
                        }
                    }
                }
            }
            catch { }
        }

        /// <summary>
        /// Remove the socket contained in the given state object
        /// from the connected array list and hash table, then close the
        /// socket
        /// </summary>
        virtual protected void CloseSocket(HttpSocket state)
        {
            HttpSocket actual_state;
            lock (ConnectedSockets)
            {
                if (!ConnectedSockets.TryGetValue(state.id, out actual_state))
                    return;

                System.Diagnostics.Debug.Assert(actual_state == state);
                ConnectedSockets.Remove(state.id);
            }

            state.CloseSocket();
        }

        /// <summary>
        /// Spawn a thread that listens to incoming connections
        /// </summary>
        public void Start(OnNewClient onConnection)
        {
            InitListenException = null;
            InitListenFinished.Reset();
            IsListening = false;
            IsShuttingDown = false;
            OnClientStart = onConnection;

            ListeningThread = new Thread(StartThread);
            ListeningThread.Name = "ListenTCP";
            ListeningThread.IsBackground = true;
            ListeningThread.Start();

            const int cleanTimeout = 300 * 1000; // in ms
            CleanTimer = new Timer(new TimerCallback(CheckSockets), null,
                cleanTimeout, cleanTimeout);
        }

        /// <summary>
        /// Open a listener socket and wait for connections
        /// </summary>
        void StartListening(ref Socket ListeningSocket)
        {
            // Note: Do not catch exceptions until we reach the main
            // listening loop, because <c>StartThread</c> should
            // intercept initialization exceptions.

            // Establish the local endpoint for the socket (only on localhost)
            IPAddress lb = (BindAddress == null)
                ? (UseIPv6 ? IPAddress.IPv6Loopback : IPAddress.Loopback)
                : BindAddress;
            IPEndPoint localEndPoint = new IPEndPoint(lb, this.LocalPort);

            // Create a TCP/IP socket
            AddressFamily af = UseIPv6 ? AddressFamily.InterNetworkV6 :
                AddressFamily.InterNetwork;
            ListeningSocket = new Socket(af, SocketType.Stream,
                ProtocolType.Tcp);

            log.Info("Listening to incoming IPv" +
                (UseIPv6 ? "6" : "4") + " connections on port " + LocalPort);

            // Bind the socket to the local endpoint and listen for incoming
            // connections.
            ListeningSocket.Bind(localEndPoint);
            ListeningSocket.Listen(1000);

            // Notify that the listening thread is up and running
            IsListening = true;
            InitListenFinished.Set();

            // Main listening loop starts now
            try
            {
                while (!IsShuttingDown)
                {
#if DEBUG_ACCEPT_CONNECTION
                    log.Debug("Reset signal");
#endif

                    ListenThreadSwitch.Reset();
                    if (IsShuttingDown)
                        break;

#if DEBUG_ACCEPT_CONNECTION
                    log.Debug("BeginAccept (before)");
#endif

                    ListeningSocket.BeginAccept(
                        new AsyncCallback(AcceptCallback), ListeningSocket);

#if DEBUG_ACCEPT_CONNECTION
                    log.Debug("Wait signal");
#endif

                    // Wait until a connection is made before continuing
                    ListenThreadSwitch.WaitOne();

#if DEBUG_ACCEPT_CONNECTION
                    log.Debug("Received signal");
#endif
                }
            }
            catch (Exception e)
            {
                log.Error(e);
            }
            finally
            {
                log.Debug("Stopped listening on port " + LocalPort);
            }
        }

        void StartThread()
        {
            Socket ListeningSocket = null;
            try
            {
                StartListening(ref ListeningSocket);
            }
            catch (Exception e)
            {
                log.Error(e);
                IsListening = false;
                InitListenException = e;
                InitListenFinished.Set();
                ListenThreadSwitch.Set();
            }
            finally
            {
                if (ListeningSocket != null)
                    ListeningSocket.Close();
            }
        }

        /// <summary>
        /// Stop the listening threads and close the client sockets
        /// </summary>
        public void Stop()
        {
            if (ListeningThread == null)
                return;

            log.Debug("Shutting down server");
            IsShuttingDown = true;

            ListenThreadSwitch.Set();

            CleanTimer.Dispose();
            CleanTimer = null;

            if (ListeningThread.IsAlive)
            {
                // Create a connection to the port to unblock the
                // listener thread
                using (var sock = new Socket(AddressFamily.Unspecified,
                    SocketType.Stream, ProtocolType.Tcp))
                {
                    try
                    {
                        sock.Connect(new IPEndPoint(IPAddress.Loopback,
                            this.LocalPort));
                        sock.Close();
                    } catch { /* ignore */ }
                }

                if (ListeningThread.ThreadState == ThreadState.WaitSleepJoin)
                    ListeningThread.Interrupt();
                Thread.Sleep(1000);
                ListeningThread.Abort();
            }
            
            ListeningThread = null;
            IsListening = false;

            log.Info("Server stopped");
        }
    }
}
