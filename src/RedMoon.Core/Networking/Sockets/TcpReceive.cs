// Red Moon: one bounded framing implementation shared by the three legacy socket roles.
using System;
using System.Net.Sockets;

namespace RedMoon.Core.Networking.Sockets
{
    internal static class TcpReceive
    {
        private static void Close(AsynchronousState state)
        {
            try
            {
                if (state?.Client is Passport passport) passport.Disconnect();
                else state?.Socket?.Close();
            }
            catch (Exception error)
            {
                // Application cleanup failures are logged, not allowed to escape an
                // asynchronous I/O callback and terminate every other connection.
                Console.WriteLine("Connection cleanup failed: " + error.GetType().Name);
            }
        }

        private static void Next(AsynchronousState state, AsyncCallback header)
        {
            if (state.Socket == null || !state.Socket.Connected) return;
            state.HeaderBytesRead = 0;
            state.Buffer = new byte[4];
            state.Socket.BeginReceive(state.Buffer, 0, 4, SocketFlags.None, header, state);
        }

        internal static void Header(IAsyncResult result, int footer, int maxBody, int maxId,
            AsyncCallback header, AsyncCallback body, Action<AsynchronousState> receive)
        {
            var state = result.AsyncState as AsynchronousState;
            try
            {
                if (!(state?.Client is Passport passport)) { Close(state); return; }
                int count = state.Socket.EndReceive(result);
                if (count <= 0) { Close(state); return; }
                state.HeaderBytesRead += count;
                if (state.HeaderBytesRead < 4)
                {
                    state.Socket.BeginReceive(state.Buffer, state.HeaderBytesRead, 4 - state.HeaderBytesRead,
                        SocketFlags.None, header, state);
                    return;
                }
                if (state.HeaderBytesRead != 4) { Close(state); return; }
                state.HeaderBytesRead = 0;
                byte[] clear = passport.Cipher?.Decrypt(state.Buffer, 4) ?? state.Buffer;
                int length = BitConverter.ToUInt16(clear, 0);
                int type = BitConverter.ToUInt16(clear, 2);
                int bodyLength = length - 4 + footer;
                if (length < 4 || footer < 0 || bodyLength < 0 || bodyLength > maxBody || type > maxId)
                { Close(state); return; }
                passport.Packet = new byte[4 + bodyLength];
                Buffer.BlockCopy(clear, 0, passport.Packet, 0, 4);
                passport.CurrentWritePosition = 4;
                passport.ExpectedReceiveLength = bodyLength;
                if (bodyLength == 0)
                {
                    receive(state);
                    Next(state, header);
                    return;
                }
                state.Buffer = new byte[bodyLength];
                state.Socket.BeginReceive(state.Buffer, 0, bodyLength, SocketFlags.None, body, state);
            }
            catch (Exception ex)
            {
                // Never log untrusted payloads or allow one malformed frame to kill a listener.
                if (!(ex is SocketException) && !(ex is ObjectDisposedException))
                    Console.WriteLine("Packet header rejected: " + ex.GetType().Name);
                Close(state);
            }
        }

        internal static void Body(IAsyncResult result, AsyncCallback header, AsyncCallback body,
            Action<AsynchronousState> receive)
        {
            var state = result.AsyncState as AsynchronousState;
            try
            {
                if (!(state?.Client is Passport passport)) { Close(state); return; }
                int count = state.Socket.EndReceive(result);
                if (count <= 0 || count > passport.ExpectedReceiveLength) { Close(state); return; }
                if (passport.Cipher != null)
                    passport.Cipher.Decrypt(passport.Packet, state.Buffer, count, passport.CurrentWritePosition);
                else Buffer.BlockCopy(state.Buffer, 0, passport.Packet, passport.CurrentWritePosition, count);
                passport.CurrentWritePosition += count;
                passport.ExpectedReceiveLength -= count;
                if (passport.ExpectedReceiveLength > 0)
                {
                    state.Buffer = new byte[passport.ExpectedReceiveLength];
                    state.Socket.BeginReceive(state.Buffer, 0, state.Buffer.Length, SocketFlags.None, body, state);
                    return;
                }
                receive(state);
                Next(state, header);
            }
            catch (Exception ex)
            {
                if (!(ex is SocketException) && !(ex is ObjectDisposedException))
                    Console.WriteLine("Packet body rejected: " + ex.GetType().Name);
                Close(state);
            }
        }

        internal static void Exchange(IAsyncResult result, int maximum, AsyncCallback exchange,
            AsyncCallback header, Action<AsynchronousState> receive)
        {
            var state = result.AsyncState as AsynchronousState;
            try
            {
                if (!(state?.Client is Passport passport)) { Close(state); return; }
                int count = state.Socket.EndReceive(result);
                if (count <= 0) { Close(state); return; }
                if (state.ExchangePacket == null)
                {
                    state.ExchangeBytesRead += count;
                    if (state.ExchangeBytesRead < 11)
                    {
                        state.Socket.BeginReceive(state.Buffer, state.ExchangeBytesRead, 11 - state.ExchangeBytesRead,
                            SocketFlags.None, exchange, state);
                        return;
                    }
                    if (state.ExchangeBytesRead != 11) { Close(state); return; }
                    byte[] first = passport.Cipher?.Decrypt(state.Buffer, 11) ?? state.Buffer;
                    int declared = BitConverter.ToInt32(first, 7);
                    if (declared < 13 || declared > maximum - 7) { Close(state); return; }
                    state.ExchangePacket = new byte[declared + 7];
                    Buffer.BlockCopy(first, 0, state.ExchangePacket, 0, 11);
                }
                else
                {
                    if (count > state.ExchangePacket.Length - state.ExchangeBytesRead) { Close(state); return; }
                    if (passport.Cipher != null)
                        passport.Cipher.Decrypt(state.ExchangePacket, state.Buffer, count, state.ExchangeBytesRead);
                    else Buffer.BlockCopy(state.Buffer, 0, state.ExchangePacket, state.ExchangeBytesRead, count);
                    state.ExchangeBytesRead += count;
                }
                int remaining = state.ExchangePacket.Length - state.ExchangeBytesRead;
                if (remaining > 0)
                {
                    state.Buffer = new byte[remaining];
                    state.Socket.BeginReceive(state.Buffer, 0, remaining, SocketFlags.None, exchange, state);
                    return;
                }
                passport.Packet = state.ExchangePacket;
                receive(state); // Changes the stream key before ANY following frame is read.
                state.ExchangePacket = null;
                state.ExchangeBytesRead = 0;
                Next(state, header);
            }
            catch (Exception ex)
            {
                if (!(ex is SocketException) && !(ex is ObjectDisposedException))
                    Console.WriteLine("Key exchange rejected: " + ex.GetType().Name);
                Close(state);
            }
        }
    }
}
