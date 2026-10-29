// Entry coordinates are from pinned cq_map.portal0_x/y; archived tiles verified AVAILABLE.
using RedMoon.Core.Common.Enums;

namespace RedMoon.GameServer.Gameplay.World
{
    public readonly struct VipTeleportDestination
    {
        public uint Map { get; }
        public ushort X { get; }
        public ushort Y { get; }
        public VipTeleportDestination(uint map, ushort x, ushort y) { Map = map; X = x; Y = y; }
    }
    public static class VipTeleportRoutes
    {
        public static bool TryGet(VIPTeleportLocations location, out VipTeleportDestination target)
        {
            switch (location)
            {
                case VIPTeleportLocations.TWIN_CITY:
                case VIPTeleportLocations.TC_SQUARE: target = new VipTeleportDestination(1002, 430, 380); return true;
                case VIPTeleportLocations.PHOENIX_CASTLE:
                case VIPTeleportLocations.PC_SQAURE: target = new VipTeleportDestination(1011, 193, 266); return true;
                case VIPTeleportLocations.APE_CITY:
                case VIPTeleportLocations.AC_SQUARE: target = new VipTeleportDestination(1020, 566, 565); return true;
                case VIPTeleportLocations.DESERT_CITY:
                case VIPTeleportLocations.DC_SQUARE: target = new VipTeleportDestination(1000, 499, 650); return true;
                case VIPTeleportLocations.BIRD_ILAND:
                case VIPTeleportLocations.BI_SQUARE: target = new VipTeleportDestination(1015, 717, 577); return true;
                default: target = default; return false; // Do not invent unverified hunt/portal coordinates.
            }
        }
    }
}
