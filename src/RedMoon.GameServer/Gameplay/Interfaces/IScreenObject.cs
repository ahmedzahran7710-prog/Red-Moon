// World Conquer Online Project 2.5517 - Phoenix Project Based
// This project has been created by Felipe Vieira Vendramini
// Source Infrastructure based on Phoenix Source, written by Gareth Jensen
// This source is targeted to Conquer Online, client version 5517
// 
// Computer User: Felipe Vieira
// File Created by:  Felipe Vieira Vendramini 
// zfserver v2.5517 - MsgServer - IScreenObject.cs
// Last Edit: 2016/11/23 10:30
// Created: 2016/11/23 10:30

using RedMoon.GameServer.Gameplay.Entities;
using RedMoon.GameServer.Gameplay.World;

namespace RedMoon.GameServer.Gameplay.Interfaces
{
    public interface IScreenObject
    {
        uint MapIdentity { get; set; }
        string Name { get; set; }
        ushort MapX { get; set; }
        ushort MapY { get; set; }
        Map Map { get; set; }
        uint Identity { get; }
        short Elevation { get; }

        IScreenObject FindAroundRole(uint idRole);
        void SendSpawnTo(Character pObj);
    }
}