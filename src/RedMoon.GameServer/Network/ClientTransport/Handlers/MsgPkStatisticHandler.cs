// World Conquer Online Project 2.5517 - Phoenix Project Based
// This project has been created by Felipe Vieira Vendramini
// Source Infrastructure based on Phoenix Source, written by Gareth Jensen
// This source is targeted to Conquer Online, client version 5517
// 
// Computer User: Felipe Vieira
// File Created by:  Felipe Vieira Vendramini 
// zfserver v2.5517 - MsgServer - 2220 - MsgPkStatistic.cs
// Last Edit: 2017/01/27 00:05
// Created: 2017/01/27 00:05

using RedMoon.GameServer.Structures.Entities;
using RedMoon.Core.Networking.Packets;

namespace RedMoon.GameServer.Network.ClientTransport.Handlers
{
    public static partial class Handlers
    {
        public static void HandlePkStatistic(Character pUser, MsgPkStatistic pMsg)
        {
            switch (pMsg.Subtype)
            {
                case 0:
                {

                    pUser.PkExploit.SendAll((int) pMsg.MaxValues);
                    break;
                }
                default:
                {
                    ServerKernel.Log.SaveLog(string.Format("Invalid 2220:{0} type", pMsg.Subtype));
                    break;
                }
            }
        }
    }
}