// Loopback-only component probes of ORIGINAL callbacks, not a game/login server.
// Valid synthetic frames only; no authentication, native crypto, SQL, or external host.
using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using RedMoon.Core.Networking.Sockets;

internal static class NetworkProbes
{
    private sealed class CallbackDriver : AsynchronousServerSocket
    {
        // Events intentionally remain live through asynchronous disconnect callbacks.
        internal readonly ManualResetEventSlim Received = new ManualResetEventSlim(false);
        internal readonly ManualResetEventSlim Disconnected = new ManualResetEventSlim(false);
        internal byte[] Frame;

        internal CallbackDriver() : base("loopback audit only", AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            OnClientReceive = state =>
            {
                Frame = ((Passport)state.Client).Packet.ToArray();
                Received.Set();
            };
            OnClientDisconnect = state => Disconnected.Set();
        }
    }

    internal static string Run(string mode)
    {
        var frame = new byte[] { 8, 0, 0xEC, 3, 1, 2, 3, 4 }; // Length 8, type 1004.
        using (var listener = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
        using (var sender = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp))
        using (var driver = new CallbackDriver())
        {
            listener.Bind(new IPEndPoint(IPAddress.Loopback, 0));
            listener.Listen(1);
            sender.NoDelay = true;
            sender.SendTimeout = 2000;
            sender.Connect(listener.LocalEndPoint);
            using (var receiver = listener.Accept())
            {
                var passport = new Passport(driver, receiver, null);
                var state = new AsynchronousState(receiver) { Client = passport, Buffer = new byte[4] };
                var firstHeaderCallbackCompleted = new ManualResetEventSlim(false);
                receiver.BeginReceive(state.Buffer, 0, 4, SocketFlags.None, result =>
                {
                    try { driver.AnnounceReceive(result); }
                    finally { firstHeaderCallbackCompleted.Set(); }
                }, state);
                int prefix = mode == "split-header" ? 2 : mode == "split-body" ? 6 : 8;
                SendAll(sender, frame, 0, prefix);
                if (!firstHeaderCallbackCompleted.Wait(2000))
                    return "HARNESS_TIMEOUT: header callback not completed";
                if (mode == "split-header")
                {
                    // The first EndReceive has consumed just two header bytes.
                    // BeginDisconnect completion may wait on the peer. Observe the
                    // peer's receive side too: readable EOF confirms the FIN without
                    // depending on the server's asynchronous disconnect callback.
                    if (sender.Poll(1500000, SelectMode.SelectRead))
                    {
                        try
                        {
                            var eof = new byte[1];
                            if (sender.Receive(eof, 0, 1, SocketFlags.Peek) == 0)
                                return "PEER_EOF_AFTER_2_HEADER_BYTES";
                            return "HARNESS_UNEXPECTED: driver sent response bytes";
                        }
                        catch (SocketException ex) when (ex.SocketErrorCode == SocketError.ConnectionReset)
                        {
                            return "PEER_RESET_AFTER_2_HEADER_BYTES";
                        }
                    }
                    if (driver.Disconnected.IsSet)
                        return "DISCONNECTED_AFTER_2_HEADER_BYTES";
                    SendAll(sender, frame, 2, 6);
                }
                if (mode == "split-body")
                {
                    if (!SpinWait.SpinUntil(() => passport.CurrentWritePosition == 6, 2000))
                        return "HARNESS_TIMEOUT: split body not observed";
                    SendAll(sender, frame, 6, 2);
                }
                if (!driver.Received.Wait(2000))
                    return driver.Disconnected.IsSet ? "DISCONNECTED" : "HARNESS_TIMEOUT: no complete frame";
                return BitConverter.ToString(driver.Frame);
            }
        }
    }

    private static void SendAll(Socket socket, byte[] bytes, int offset, int count)
    {
        while (count > 0)
        {
            int sent = socket.Send(bytes, offset, count, SocketFlags.None);
            if (sent <= 0) throw new InvalidOperationException("Loopback peer closed during test send");
            offset += sent;
            count -= sent;
        }
    }
}
