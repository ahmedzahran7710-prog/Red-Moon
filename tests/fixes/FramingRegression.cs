using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using ServerCore.Interfaces;
using ServerCore.Networking.Sockets;
using ServerCore.Security;

internal static class FramingRegression
{
    private sealed class Sink
    {
        internal readonly ConcurrentQueue<byte[]> Frames = new ConcurrentQueue<byte[]>();
        internal byte[] Exchange;
        internal int Closed;
        internal void Receive(AsynchronousState state) { Frames.Enqueue(((Passport)state.Client).Packet.ToArray()); }
    }
    private sealed class ServerRole : AsynchronousServerSocket
    {
        internal ServerRole(Sink s, byte[] nextKey) : base("test", AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            OnClientReceive = s.Receive; OnClientDisconnect = _ => Interlocked.Increment(ref s.Closed);
            OnClientExchange = state => { s.Exchange = ((Passport)state.Client).Packet.ToArray(); if (nextKey != null) ((Passport)state.Client).Cipher = Cipher(nextKey); };
        }
    }
    private sealed class ClientRole : AsynchronousClientSocket
    {
        internal ClientRole(Sink s) : base("test", AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        { OnClientReceive = s.Receive; OnClientDisconnect = _ => Interlocked.Increment(ref s.Closed); }
    }
    private sealed class InternalRole : AsynchronousGameServerSocket
    {
        internal InternalRole(Sink s) : base("test", AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        { OnClientReceive = s.Receive; OnClientDisconnect = _ => Interlocked.Increment(ref s.Closed); }
    }
    private static BlowfishCipher Cipher(byte[] key) { BlowfishCipher.InitialKey = key; return new BlowfishCipher(); }
    private static void Send(Socket s, byte[] bytes)
    {
        int offset = 0;
        while (offset < bytes.Length)
        { int n = s.Send(bytes, offset, bytes.Length - offset, SocketFlags.None); if (n <= 0) throw new Exception("Closed test socket"); offset += n; }
    }
    internal static string CloseListener(bool internalRole)
    {
        var sink = new Sink();
        if (internalRole)
        {
            var listener = new InternalRole(sink); listener.Bind("127.0.0.1", 0); listener.Listen(1); listener.Dispose();
        }
        else
        {
            var listener = new ServerRole(sink, null); listener.Bind("127.0.0.1", 0); listener.Listen(1); listener.Dispose();
        }
        Thread.Sleep(150); // outstanding Accept callback must return, not restart a disposed listener
        return "True";
    }
    internal static string Run(string role, string mode)
    {
        var sink = new Sink();
        byte[] firstKey = Enumerable.Range(0, 16).Select(x => (byte)x).ToArray();
        byte[] nextKey = Enumerable.Range(16, 16).Select(x => (byte)x).ToArray();
        using (Socket driver = role == "client" ? (Socket)new ClientRole(sink) : role == "internal" ? new InternalRole(sink) : new ServerRole(sink, nextKey))
        using (var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
        using (var peer = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
        {
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0)); listener.Listen(1);
            peer.NoDelay = true; peer.SendTimeout = 2000; peer.Connect(listener.LocalEndPoint);
            using (Socket receive = listener.Accept())
            {
                bool exchange = mode == "exchange";
                var passport = new Passport((IAsynchronousSocket)driver, receive, exchange ? Cipher(firstKey) : null);
                var state = new AsynchronousState(receive) { Client = passport, Buffer = new byte[exchange ? 11 : 4] };
                var consumed = new ManualResetEventSlim(false);
                AsyncCallback callback = role == "client" ? ((ClientRole)driver).AnnounceReceive : role == "internal" ? ((InternalRole)driver).AnnounceReceive : ((ServerRole)driver).AnnounceReceive;
                if (exchange) callback = ((ServerRole)driver).PrepareReceive;
                receive.BeginReceive(state.Buffer, 0, state.Buffer.Length, SocketFlags.None, ar => { try { callback(ar); } finally { consumed.Set(); } }, state);
                if (mode == "disconnect")
                {
                    passport.Disconnect(); passport.Disconnect();
                    return (sink.Closed == 1 && passport.IsDisconnected).ToString();
                }
                byte[] frame = { 8, 0, 0xEC, 3, 1, 2, 3, 4 };
                if (mode == "invalid-small" || mode == "invalid-large" || mode == "invalid-type")
                {
                    ushort length = mode == "invalid-small" ? (ushort)3 : mode == "invalid-large" ? (ushort)65535 : (ushort)8;
                    ushort type = mode == "invalid-type" ? (ushort)65535 : (ushort)1004;
                    Send(peer, BitConverter.GetBytes(length).Concat(BitConverter.GetBytes(type)).ToArray());
                    if (!peer.Poll(2000000, SelectMode.SelectRead)) return "TIMEOUT";
                    try { return peer.Receive(new byte[1], SocketFlags.Peek) == 0 ? "REJECTED" : "UNEXPECTED_RESPONSE"; }
                    catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset) { return "REJECTED"; }
                }
                if (exchange)
                {
                    // A bounded synthetic client-DH envelope followed by a frame under
                    // a NEW key in the SAME send: no bytes may be decrypted with the old key.
                    byte[] envelope = new byte[147];
                    Buffer.BlockCopy(BitConverter.GetBytes(140), 0, envelope, 7, 4);
                    Buffer.BlockCopy(BitConverter.GetBytes(0), 0, envelope, 11, 4);
                    Buffer.BlockCopy(BitConverter.GetBytes(128), 0, envelope, 15, 4);
                    Array.Fill(envelope, (byte)'A', 19, 128);
                    byte[] encoded = Cipher(firstKey).Encrypt(envelope, envelope.Length);
                    Send(peer, encoded.Take(3).ToArray());
                    if (!consumed.Wait(2000)) return "TIMEOUT";
                    Send(peer, encoded.Skip(3).Concat(Cipher(nextKey).Encrypt(frame, frame.Length)).ToArray());
                    if (!SpinWait.SpinUntil(() => sink.Frames.Count == 1, 2000)) return "TIMEOUT";
                    return (sink.Exchange.SequenceEqual(envelope) && sink.Frames.Single().SequenceEqual(frame)).ToString();
                }
                if (mode == "split")
                {
                    Send(peer, frame.Take(1).ToArray());
                    if (!consumed.Wait(2000)) return "TIMEOUT";
                    Send(peer, frame.Skip(1).ToArray());
                    if (!SpinWait.SpinUntil(() => sink.Frames.Count == 1, 2000)) return "TIMEOUT";
                    return sink.Frames.Single().SequenceEqual(frame).ToString();
                }
                Send(peer, frame.Concat(frame).ToArray());
                if (!SpinWait.SpinUntil(() => sink.Frames.Count == 2, 2000)) return "TIMEOUT";
                return sink.Frames.All(x => x.SequenceEqual(frame)).ToString();
            }
        }
    }
}
