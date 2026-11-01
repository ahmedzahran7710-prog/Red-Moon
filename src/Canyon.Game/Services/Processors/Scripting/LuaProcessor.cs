using Canyon.Database.Entities;
using Canyon.Game.Scripting;
using Canyon.Game.Scripting.Attributes;
using Canyon.Game.Services.Managers;
using Canyon.Game.States;
using Canyon.Game.States.Items;
using Canyon.Game.States.Syndicates;
using Canyon.Game.States.User;
using NLua;
using static Canyon.Game.Scripting.LuaScriptConst;

namespace Canyon.Game.Services.Processors.Scripting
{
    public sealed partial class LuaProcessor
    {
        private static readonly ILogger logger = LogFactory.CreateLogger<LuaProcessor>();

        private readonly Lua lua;
        private readonly int threadIndex;
        private object syncObject = new object();

        private string currentScript;
        private Character user;
        private Role role;
        private Item item;
        private string input;

        public LuaProcessor(int threadIndex)
        {
            this.threadIndex = threadIndex;
            lua = new Lua();
            Initialize();
        }

        private static string ResolveScriptPath(string rawRelativePath)
        {
            if (string.IsNullOrWhiteSpace(rawRelativePath))
            {
                return string.Empty;
            }

            string[] splitPath = rawRelativePath.Split(new[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (splitPath.Length == 0)
            {
                return string.Empty;
            }

            string luaRoot = Path.Combine(Environment.CurrentDirectory, "lua");
            string directPath = Path.Combine(luaRoot, Path.Combine(splitPath));
            if (File.Exists(directPath))
            {
                return directPath;
            }

            if (splitPath.Length > 1 && splitPath[0].Equals("V100", StringComparison.OrdinalIgnoreCase))
            {
                string flattenedPath = Path.Combine(luaRoot, Path.Combine(splitPath[1..]));
                if (File.Exists(flattenedPath))
                {
                    return flattenedPath;
                }
            }
            else
            {
                string nestedPath = Path.Combine(luaRoot, "V100", Path.Combine(splitPath));
                if (File.Exists(nestedPath))
                {
                    return nestedPath;
                }
            }

            return directPath;
        }

        private void Initialize()
        {
            RegisterLocalFunctions();

            foreach (var item in LuaScriptsSettings.Settings.MOD
                .OrderBy(x => int.TryParse(x.Key, out int keyId) ? keyId : int.MaxValue)
                .ThenBy(x => x.Key, StringComparer.Ordinal))
            {
                string realPath = ResolveScriptPath(item.Value);
                if (string.IsNullOrEmpty(realPath) || !File.Exists(realPath))
                {
                    logger.LogWarning("Script file \"{path}\" not found!", item.Value);
                    continue;
                }

                lua.DoFile(realPath);
            }
        }

        private void RegisterLocalFunctions()
        {
            foreach (var method in GetType().GetMethods().Where(x => x.IsPublic))
            {
                var customAttributes = method.GetCustomAttributes(false);
                if (customAttributes.All(x => x is not LuaFunctionAttribute))
                {
                    // skip not mapped
                    continue;
                }

#if DEBUG
                logger.LogDebug("Lua function [{}] registered!", method.Name);
#endif

                lua.RegisterFunction(method.Name, this, method);
            }
        }

        public void ReloadScript(int idScript)
        {
            lock (syncObject)
            {
                string matchedFile = null;
                if (!LuaScriptsSettings.Settings.MOD.TryGetValue(idScript.ToString(), out matchedFile))
                {
                    matchedFile = LuaScriptsSettings.Settings.MOD
                        .FirstOrDefault(x => int.TryParse(x.Key, out int parsed) && parsed == idScript)
                        .Value;
                }

                if (!string.IsNullOrEmpty(matchedFile))
                {
                    string realPath = ResolveScriptPath(matchedFile);
                    if (string.IsNullOrEmpty(realPath) || !File.Exists(realPath))
                    {
                        logger.LogWarning("Script file \"{path}\" not found!", realPath);
                        return;
                    }
                    lua.DoFile(realPath);
                }
            }
        }

        public void ReloadScripts()
        {
            lock (syncObject)
            {
                foreach (var file in LuaScriptsSettings.Settings.MOD
                    .OrderBy(x => int.TryParse(x.Key, out int keyId) ? keyId : int.MaxValue)
                    .ThenBy(x => x.Key, StringComparer.Ordinal))
                {
                    string realPath = ResolveScriptPath(file.Value);
                    if (string.IsNullOrEmpty(realPath) || !File.Exists(realPath))
                    {
                        logger.LogWarning("Script file \"{path}\" not found!", realPath);
                        continue;
                    }
                    lua.DoFile(realPath);
                }
            }
        }

        public bool Execute(Character user, Role role, Item item, string input, string script)
        {
            try
            {
                lock (syncObject)
                {
                    currentScript = script;
                    this.user = user;
                    this.role = role;
                    this.item = item;
                    this.input = input;

                    lua.DoString(script);

                    currentScript = string.Empty;
                    this.user = null;
                    this.role = null;
                    this.item = null;
                    this.input = string.Empty;
                    return true;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(new EventId(threadIndex), ex, "An error occurred when executing LUA Scripts!!! [{}]: {}", script, ex.Message);
                return false;
            }
        }

        #region Team

        [LuaFunction]
        public int GetTeamAttr(int userId, int index)
        {
            Character user = GetUser(userId);
            if (user?.Team == null)
            {
                return 0;
            }

            switch (index)
            {
                case G_TEAM_Alive:
                    {
                        return user.Team.Members.Count(x => x.IsAlive);
                    }
                case G_TEAM_Friend:
                    {
                        if (user.Team.MemberCount == 1)
                        {
                            return 0;
                        }

                        foreach (var member in user.Team.Members)
                        {
                            if (member.Identity == userId)
                            {
                                continue;
                            }

                            if (!user.IsFriend(member.Identity))
                            {
                                return 0;
                            }
                        }
                        return 1;
                    }
                case G_TEAM_Mate:
                    {
                        if (user.Team.MemberCount != 2)
                        {
                            return 0;
                        }

                        return user.Team.Members.Any(x => x.Identity != userId && x.IsMate(user.Identity)) ? 1 : 0;
                    }
                case G_TEAM_MaxLev:
                    {
                        return user.Team.Members.Max(x => x.Level);
                    }
                case G_TEAM_MinLev:
                    {
                        return user.Team.Members.Min(x => x.Level);
                    }
                case G_TEAM_Amount:
                    {
                        return user.Team.MemberCount;
                    }
                case G_TEAM_ID:
                    {
                        return (int)user.Team.TeamId;
                    }
            }

            return 0;
        }

        #endregion

        #region ItemType

        private DbItemtype GetItemType(int itemType)
        {
            if (itemType <= 0)
            {
                return item?.Itemtype;
            }
            else
            {
                return ItemManager.GetItemtype((uint)itemType);
            }
        }

        [LuaFunction]
        public long GetItemTypeInt(int itemType, int index)
        {
            DbItemtype it = GetItemType(itemType);
            if (it == null)
            {
                return 0;
            }

            switch (index)
            {
                case G_ITEMTYPE_Profession: return it.ReqProfession;
                case G_ITEMTYPE_Skill: return it.ReqWeaponskill;
                case G_ITEMTYPE_Level: return it.ReqLevel;
                case G_ITEMTYPE_Sex: return it.ReqSex;
                case G_ITEMTYPE_Monopoly: return it.Monopoly;
                case G_ITEMTYPE_Mask: return it.TypeMask;
                case G_ITEMTYPE_EmoneyPrice: return it.EmoneyPrice;
                case G_ITEMTYPE_EmoneyMonoPrice: return it.BoundEmoneyPrice;
                case G_ITEMTYPE_SaveTime: return it.SaveTime;
                case G_ITEMTYPE_AccumulateLimit: return it.AccumulateLimit;
                default: return 0;
            }
        }

        [LuaFunction]
        public string GetItemTypeStr(int itemType, int index)
        {
            DbItemtype it = GetItemType(itemType);
            if (it == null)
            {
                return string.Empty;
            }

            switch (index)
            {
                case G_ITEMTYPE_Name: return it.Name;
                case G_ITEMTYPE_TypeDesc: return it.TypeDesc;
                case G_ITEMTYPE_ItemDesc: return it.ItemDesc;
                default: return string.Empty;
            }
        }

        #endregion

        #region Trap

        private MapTrap GetMapTrap(int trapId)
        {
            if (trapId <= 0)
            {
                return role as MapTrap;
            }
            else
            {
                return RoleManager.GetRole<MapTrap>((uint)trapId);
            }
        }

        [LuaFunction]
        public long GetMapTrapInt(int trapId, int index)
        {
            MapTrap mapTrap = GetMapTrap(trapId);
            if (mapTrap == null)
            {
                return 0;
            }

            switch (index)
            {
                case G_TRAP_ID: return mapTrap.Identity;
                case G_TRAP_TYPE: return mapTrap.Type;
                case G_TRAP_LOOK: return mapTrap.Mesh;
                case G_TRAP_OWNER_ID: return mapTrap.OwnerIdentity;
                case G_TRAP_MAPID: return mapTrap.MapIdentity;
                case G_TRAP_PosX: return mapTrap.X;
                case G_TRAP_PosY: return mapTrap.Y;
                case G_TRAP_DATA:
                case G_TRAP_BOUND_CX:
                case G_TRAP_BOUND_CY:
                default: return 0;
            }
        }

        [LuaFunction]
        public int GetTrapCount(int type)
        {
            return RoleManager.QueryRoleByType<MapTrap>().Count(x => x.Type == type);
        }

        #endregion

        #region Syndicate

        private Syndicate GetSyndicate(int synId, int idUser)
        {
            if (synId == 0)
            {
                Character user = GetUser(idUser);
                if (user?.Syndicate != null)
                {
                    return user.Syndicate;
                }
                return this.user?.Syndicate;
            }
            return SyndicateManager.GetSyndicate(synId);
        }

        [LuaFunction]
        public long GetSynInt(int synId, int index, int idUser)
        {
            Syndicate syndicate = GetSyndicate(synId, idUser);
            if (syndicate == null)
            {
                return 0;
            }

            switch (index)
            {
                case G_SYNDICATE_MONEY:
                    {
                        return syndicate.Money;
                    }
                case G_SYNDICATE_MEMBER_AMOUNT:
                    {
                        return syndicate.MemberCount;
                    }
                case G_SYNDICATE_EMONEY:
                    {
                        return syndicate.ConquerPoints;
                    }
                case G_SYNDICATE_LEVEL:
                    {
                        return syndicate.Level;
                    }
            }
            return 0;
        }

        [LuaFunction]
        public string GetSynStr(int synId, int index, int idUser)
        {
            Syndicate syndicate = GetSyndicate(synId, idUser);
            if (syndicate == null)
            {
                return StrNone;
            }

            switch (index)
            {
                case G_SYNDICATE_NAME:
                    {
                        return syndicate.Name;
                    }
                case G_SYNDICATE_LEADER_NAME:
                    {
                        return syndicate.Leader?.UserName ?? StrNone;
                    }
            }
            return StrNone;
        }

        [LuaFunction]
        public long GetSynMemberInt(int idUser, int index)
        {
            Character user = GetUser(idUser);
            if (user == null)
            {
                return 0;
            }

            switch (index)
            {
                case G_SYN_MEMBER_ATTR_RANK:
                    {
                        return (long)user.SyndicateRank;
                    }
                case G_SYN_MEMBER_ATTR_PROFFER:
                    {
                        return user.SyndicateMember?.Silvers ?? 0;
                    }
            }

            return 0;
        }

        [LuaFunction]
        public string GetSynMemberStr(int idUser, int index)
        {
            Character user = GetUser(idUser);
            if (user == null)
            {
                return StrNone;
            }

            switch (index)
            {
                case G_SYN_MEMBER_ATTR_RANK:
                    {
                        return user.SyndicateRankName;
                    }
            }

            return StrNone;
        }

        [LuaFunction]
        public int GetBrickQuality(int idUser)
        {
            logger.LogWarning("GetBrickQuality(int idUser) not implemented");
            return 0;
        }

        [LuaFunction]
        public int GetVexillumRank(int idSyn)
        {
            logger.LogWarning("GetVexillumRank(int idSyn) not implemented");
            return 0;
        }

        #endregion
    }
}
