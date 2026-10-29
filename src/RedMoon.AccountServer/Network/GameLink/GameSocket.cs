using System;
using System.Net.Sockets;
using RedMoon.Core.Common;
using RedMoon.Core.Networking.Packets;
using RedMoon.Core.Networking.Sockets;

namespace RedMoon.AccountServer.Network.GameLink
{
    public sealed class MsgServerSocket : AsynchronousGameServerSocket
    {
        private readonly PacketProcessor<PacketHandlerType, PacketType, Action<GameServer, byte[]>> m_packetProcessor;

        public MsgServerSocket()
            : base("", AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
        {
            OnClientConnect = Connect;
            OnClientReceive = Receive;
            OnClientDisconnect = Disconnect;

            m_packetProcessor = new PacketProcessor<PacketHandlerType, PacketType, Action<GameServer, byte[]>>(new MsgPacketHandler());
        }

        public void Connect(AsynchronousState pState)
        {
            var pClient = new GameServer(this, pState.Socket, null);
            pState.Client = pClient;
        }

        public void Receive(AsynchronousState pState)
        {
            var pServer = pState.Client as GameServer;
            if (pServer != null && pServer.Packet != null)
            {
                var type = (PacketType)BitConverter.ToUInt16(pServer.Packet, 2);
                Action<GameServer, byte[]> action = m_packetProcessor[type];

                // Process the client's packet:
                if (action != null) action(pServer, pServer.Packet);
                else MsgPacketHandler.Report(pServer.Packet);
            }
        }

        public void Disconnect(object pObj)
        {
            var pServer = pObj as GameServer;
            if (pServer == null) return;

            string name = pServer.ServerName;
            if (!string.IsNullOrEmpty(name))
                ((System.Collections.Generic.ICollection<System.Collections.Generic.KeyValuePair<string, GameServer>>)ServerKernel.OnlineServers)
                    .Remove(new System.Collections.Generic.KeyValuePair<string, GameServer>(name, pServer));
            ServerKernel.Log.SaveLog(string.Format("Server [{0}] has disconnected.", name ?? "unregistered"), true, LogType.WARNING);
        }
    }
}