// World Conquer Online Project 2.5517 - Phoenix Project Based
// This project has been created by Felipe Vieira Vendramini
// Source Infrastructure based on Phoenix Source, written by Gareth Jensen
// This source is targeted to Conquer Online, client version 5517
// 
// Computer User: Felipe Vieira
// File Created by:  Felipe Vieira Vendramini 
// zfserver v2.5517 - MsgServer - 1128 - MsgVipUserHandle.cs
// Last Edit: 2017/01/27 17:30
// Created: 2017/01/27 17:28

using System;
using RedMoon.GameServer.Gameplay.Entities;
using RedMoon.GameServer.Gameplay.World;
using RedMoon.Core.Common.Enums;
using RedMoon.Core.Networking.Packets;

namespace RedMoon.GameServer.Network.ClientTransport.Handlers
{
    public static partial class Handlers
    {
        public static void HandleVipUserHandle(Character pUser, MsgVipUserHandle pMsg)
        {
            if (pUser?.Owner == null || pUser.Map == null || pUser.Owner.VipLevel == 0) return;
            if (pUser.Map.IsPrisionMap() || pUser.Map.IsPkField() || pUser.Map.IsSynMap() || pUser.Map.IsFamilyMap()
                || pUser.Map.IsTeleportDisable() || pUser.Map.IsChgMapDisable()) return;
            if (pMsg.Type != VIPTeleportTypes.SELF_TELEPORT)
            {
                pUser.Send("Team VIP teleport is not available yet.");
                return;
            }
            if (!VipTeleportRoutes.TryGet(pMsg.Location, out var route))
            {
                pUser.Send("This VIP destination has not been configured yet.");
                return;
            }
            if (!ServerKernel.Maps.TryGetValue(route.Map, out var map) || !map.IsValidPoint(route.X, route.Y)
                || map[route.X, route.Y].Access < TileType.ITEM)
            {
                pUser.Send("That teleport destination is temporarily unavailable.");
                return;
            }
            // Existing cooldown getters advance the timer; validate first so a rejected
            // unsupported request cannot consume a successful teleport's cooldown.
            if (pMsg.Location > VIPTeleportLocations.BIRD_ILAND)
            {
                if (!pUser.CanUserPortalTeleport)
                {
                    pUser.Send(string.Format(ServerString.STR_VIPTELE_PORTAL_REMAIN, pUser.PortalTeleportWaitTime));
                    return;
                }
            }
            else if (!pUser.CanUseCityTeleport)
            {
                pUser.Send(string.Format(ServerString.STR_VIPTELE_CITY_REMAIN, pUser.CityTeleportWaitTime));
                return;
            }
            pUser.ChangeMap(route.X, route.Y, route.Map);
        }
    }
}
