// World Conquer Online Project 2.5517 - Phoenix Project Based
// This project has been created by Felipe Vieira Vendramini and updated by Cristian Ocaña Soler
// Source Infrastructure based on Phoenix Source, written by Gareth Jensen
// This source is targeted to Conquer Online, client version 5517
// 

// File Created by:  Felipe Vieira Vendramini 
// zfserver v2.5517 - ServerCore - Asynchronous Client Socket.cs
// Last Edit: 2016/11/23 07:59
// Created: 2016/11/23 07:52

using System;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using ServerCore.Common;
using ServerCore.Interfaces;
using ServerCore.Networking.Packets;

namespace ServerCore.Networking.Sockets
{
    /// <summary>
    /// The asynchronous client class encapsulates an asynchronous client socket. It allows the client to connect to a 
    /// server and process packets asynchronously. It makes use of Microsoft's .NET socket class to control asynchronous 
    /// packet processing requests. 
    /// </summary>
    public unsafe class AsynchronousClientSocket : Socket, IAsynchronousSocket
    {
        // Socket Events - Pure virtual functions (Defined when the abstract class is constructed):
        protected delegate void AsynchronousConnect(AsynchronousState state);     // Invoked on a successful server connect.
        protected delegate void AsynchronousExchange(AsynchronousState state);    // Invoked to exchange keys with the server.
        protected delegate void AsynchronousReceive(AsynchronousState state);     // Invoked to receive data from the server.
        protected delegate void AsynchronousDisconnect(object state);             // Invoked when the server has disconnected.

        // Protected Variable Declarations:
        protected AsynchronousConnect OnClientConnect;         // Invoked on a successful client accept.
        protected AsynchronousExchange OnClientExchange;       // Invoked to exchange keys with the client.
        protected AsynchronousReceive OnClientReceive;         // Invoked to receive data from the client.
        protected AsynchronousDisconnect OnClientDisconnect;   // Invoked when the client has disconnected.

        // Global-Scope Properties & Constants.
        public string Name { get; set; }                // The name of the server.
        public int FooterLength { get; set; }           // The length of the footer for each packet.
        public string Footer { get; set; }              // The text for the footer at the end of each packet.
        public const int EXCHANGE_BUFFER_SIZE = 1024;   // The largest possible length of the exchange buffer.
        public const int MAX_PACKET_SIZE = 2048;        // The largest possible length of a data packet.

        /// <summary>
        /// The asynchronous client class encapsulates an asynchronous client socket. It allows the client to connect to a 
        /// server and process packets asynchronously. It makes use of Microsoft's .NET socket class to control asynchronous 
        /// packet processing requests. 
        /// </summary>
        /// <param name="name">The name of the client.</param>
        /// <param name="family">Specifies the addressing scheme that the socket instance will use.</param>
        /// <param name="socketType">Specifies the socket type that the instance represents.</param>
        /// <param name="protocol">Specifies the protocol the socket instance will support.</param>
        public AsynchronousClientSocket(string name, AddressFamily family, SocketType socketType, ProtocolType protocol)
            : base(family, socketType, protocol)
        {
            Name = name;
            FooterLength = 0;
            Footer = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                base.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            }
            base.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        }

        /// <summary>
        /// The asynchronous client class encapsulates an asynchronous client socket. It allows the client to connect to a 
        /// server and process packets asynchronously. It makes use of Microsoft's .NET socket class to control asynchronous 
        /// packet processing requests. 
        /// </summary>
        /// <param name="name">The name of the client.</param>
        /// <param name="family">Specifies the addressing scheme that the socket instance will use.</param>
        /// <param name="socketType">Specifies the socket type that the instance represents.</param>
        /// <param name="protocol">Specifies the protocol the socket instance will support.</param>
        public AsynchronousClientSocket(string name, AddressFamily family, SocketType socketType, ProtocolType protocol, Action<AsynchronousState> connect, Action<AsynchronousState> receive, Action<object> disconnect)
            : base(family, socketType, protocol)
        {
            Name = name;
            FooterLength = 0;
            Footer = "";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                base.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            }
            base.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
            OnClientConnect = new AsynchronousConnect(connect);
            OnClientDisconnect = new AsynchronousDisconnect(disconnect);
            OnClientReceive = new AsynchronousReceive(receive);
        }

        /// <summary>
        /// The asynchronous client class encapsulates an asynchronous client socket. It allows the client to connect to a 
        /// server and process packets asynchronously. It makes use of Microsoft's .NET socket class to control asynchronous 
        /// packet processing requests. 
        /// </summary>
        /// <param name="name">The name of the client.</param>
        /// <param name="footer">The footer for each packet.</param>
        /// <param name="family">Specifies the addressing scheme that the socket instance will use.</param>
        /// <param name="socketType">Specifies the socket type that the instance represents.</param>
        /// <param name="protocol">Specifies the protocol the socket instance will support.</param>
        public AsynchronousClientSocket(string name, string footer, AddressFamily family, SocketType socketType,
            ProtocolType protocol)
            : base(family, socketType, protocol)
        {
            Name = name;
            FooterLength = footer.Length;
            Footer = footer;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                base.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.DontLinger, true);
            }
            base.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.KeepAlive, true);
        }

        /// <summary>
        /// This method associates the socket with an IP address and a port. It does this by creating an IP endpoint using
        /// the local address and port, and requesting a remote IP endpoint using the specified IP address and port. It 
        /// will make an attempt to connect to the server specified.
        /// </summary>
        /// <param name="ip">The IP address the client will attempt to connect to.</param>
        /// <param name="port">The port the socket will connect to.</param>
        public bool ConnectTo(string ip, int port)
        {
            try
            {
                // Make an attempt to connect to the server:
                AsynchronousState state = new AsynchronousState(this);
                BeginConnect(ip, port, CompleteConnect, state);
                state.Event.WaitOne();
                return Connected;
            }
            catch /*(SocketException exception)*/ { /*Console.WriteLine(exception);*/ }
            return false;
        }

        /// <summary>
        /// This method completes the connection attempt. If the attempt failed, an exception will be thrown; else,
        /// the connection event will be called and the client will begin sending data to and receiving data from the 
        /// connected server.
        /// </summary>
        /// <param name="result">Represents the status of an asynchronous operation.</param>
        private void CompleteConnect(IAsyncResult result)
        {
            try
            {
                // Accept the socket from the asynchronous result:
                AsynchronousState state = result.AsyncState as AsynchronousState;

                // If the socket is valid, complete the connection attempt.
                if (state != null && state.Socket != null)
                {
                    // Complete the connection attempt:
                    try { state.Socket.EndConnect(result); }
                    catch /*(SocketException exception)*/ { /*Console.WriteLine(exception);*/ }
                    finally { state.Event.Set(); }

                    // If the connection was not successful, discontinue; else, handle the on connect event:
                    if (!state.Socket.Connected) return;
                    if (OnClientConnect != null) OnClientConnect(state);

                    // Begin receiving data from the server. If the exchange socket event is not null,
                    // handle the key exchange for the packet cipher; else, accept data from the server.
                    if (OnClientExchange != null)
                    {
                        state.Buffer = new byte[11];
                        state.Socket.BeginReceive(state.Buffer, 0, 11,
                            SocketFlags.None, PrepareReceive, state);
                    }
                    else if (OnClientReceive != null)
                    {
                        //Console.WriteLine("Starting recv process...");
                        // The server does not process key exchange. Start receiving packets immediately.
                        state.Buffer = new byte[4];
                        state.Socket.BeginReceive(state.Buffer, 0, 4,
                            SocketFlags.None, AnnounceReceive, state);
                    }
                    else
                    {
                        // There are no processing methods for this server!
                        Console.WriteLine("No processing methods defined for the " + Name);
                        state.Socket.Disconnect(false);
                    }
                }
            }
            catch (SocketException e)
            {
                // Was the connection issue a problem on the client's side or the server's side?
                if (e.SocketErrorCode != SocketError.Disconnecting &&
                    e.SocketErrorCode != SocketError.NotConnected &&
                    e.SocketErrorCode != SocketError.ConnectionReset &&
                    e.SocketErrorCode != SocketError.ConnectionAborted &&
                    e.SocketErrorCode != SocketError.Shutdown)
                    Console.WriteLine(e);
            }
        }

        /// <summary>
        /// This method begins the client and server key exchange. The client sends key exchange data first, which is
        /// picked up by the server in this socket event and processed. Then, the exchange packet is sent back to the
        /// client so both the client and server have matching cipher keys, and packets are sent to the server.
        /// </summary>
        /// <param name="result">Represents the status of an asynchronous operation.</param>
        public void PrepareReceive(IAsyncResult result)
        {
            TcpReceive.Exchange(result, EXCHANGE_BUFFER_SIZE, PrepareReceive,
                AnnounceReceive, state => OnClientExchange(state));
        }

        /// <summary>
        /// This method begins the receiving of a packet from the server's socket. It receives the first four bytes
        /// of a packet, then requests to receive the body of the packet. If the length of the packet is less than
        /// the length of the packet header, the client will be disconnected from the server.
        /// </summary>
        /// <param name="result">Represents the status of an asynchronous operation.</param>
        public void AnnounceReceive(IAsyncResult result)
        {
            TcpReceive.Header(result, FooterLength, MAX_PACKET_SIZE, AsynchronousServerSocket.MAX_PACKET_ID,
                AnnounceReceive, CompleteReceive, state => OnClientReceive(state));
        }

        /// <summary>
        /// This method completes the receiving of a packet from the server's socket. It attempts to receive the 
        /// remaining bytes of a packet, then requests to receive the header of the next packet. If the length of 
        /// the packet is zero, the client will be disconnected from the server. If the length of the buffer is
        /// not the expected length, then the packet is a fragment and the server will attempt to receive the
        /// completed packet again.
        /// </summary>
        /// <param name="result">Represents the status of an asynchronous operation.</param>
        public void CompleteReceive(IAsyncResult result)
        {
            TcpReceive.Body(result, AnnounceReceive, CompleteReceive, state => OnClientReceive(state));
        }

        /// <summary>
        /// This method is called once a previously connected and authenticated client has been disconnected 
        /// from the server. The method calls the disconnect socket event to dispose of the client's initialized
        /// game structures.
        /// </summary>
        /// <param name="result">Represents the status of an asynchronous operation.</param>
        // Passport owns transport shutdown; notification is synchronous and occurs once.
        void IAsynchronousSocket.OnDisconnected(Passport passport)
        {
            if (passport != null) OnClientDisconnect?.Invoke(passport);
        }

        public void Disconnect(IAsyncResult result)
        {
            // Dispose of the asynchronous state:
            if (result != null && OnClientDisconnect != null)
                OnClientDisconnect(result);
        }

        /// <summary>
        /// This method is called once a previously connected and authenticated client has been disconnected 
        /// from the server. The method calls the disconnect socket event to dispose of the client's initialized
        /// game structures.
        /// </summary>
        /// <param name="state">Represents the status of an asynchronous operation.</param>
        public void Disconnect(AsynchronousState state)
        {
            // Dispose of the asynchronous state:
            if (state != null && state.Client != null && OnClientDisconnect != null)
                OnClientDisconnect(state.Client);
        }
    }
}
