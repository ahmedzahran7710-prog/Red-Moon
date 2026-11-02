using Canyon.Login.States;
using Canyon.Network.Packets.Login;
using System.Net.Sockets;

namespace Canyon.Login.Sockets.Login.Packets
{
    public sealed class MsgPCNum : MsgPCNum<Client>
    {
        public override Task ProcessAsync(Client client)
        {
            // The peer or receive loop may already have closed this socket while
            // the queued MsgPCNum is being processed. Only ignore that close race.
            try
            {
                client.Disconnect();
            }
            catch (SocketException ex) when (ex.SocketErrorCode == SocketError.NotConnected)
            {
            }
            catch (ObjectDisposedException)
            {
            }
            return Task.CompletedTask;
        }
    }
}
