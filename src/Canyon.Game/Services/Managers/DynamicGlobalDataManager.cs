using Canyon.Database.Entities;
using Canyon.Game.Database;
using Canyon.Game.Database.Repositories;
using Canyon.Game.Scripting;
using Canyon.Game.Sockets.Ai;
using Canyon.Game.Sockets.Ai.Packets;
using Canyon.Game.Sockets.Game.Packets;
using Canyon.Game.States;
using Canyon.Game.States.Items;
using Canyon.Game.States.User;
using Canyon.Game.States.World;
using Canyon.Network.Packets.Ai;
using Microsoft.EntityFrameworkCore;
using System.Collections.Concurrent;
using System.Drawing;
using System.Text.Json;

namespace Canyon.Game.Services.Managers
{
    public class DynamicGlobalDataManager
    {
        public const int MaxParams = 6;

        private static readonly ILogger logger = LogFactory.CreateLogger<DynamicGlobalDataManager>();
        private static readonly ConcurrentDictionary<uint, DbDynaGlobalData> globalData = new();

        public static async Task InitializeAsync()
        {
            logger.LogInformation("Initializing Dynamic Global Data Manager!!!");

            var data = await DynamicGlobalDataRepository.GetAsync();
            foreach (var g in data)
            {
                globalData.TryAdd(g.Id, g);
            }

            await ServerRatesManager.InitializeAsync();
        }

        public static Task<bool> SaveAsync(DbDynaGlobalData data)
        {
            return ServerDbContext.SaveAsync(data);
        }

        public static async Task<DbDynaGlobalData> GetAsync(uint id)
        {
            if (globalData.TryGetValue(id, out var result))
            {
                return result;
            }
            result = new DbDynaGlobalData
            {
                Id = id,
                Datastr0 = string.Empty,
                Datastr1 = string.Empty,
                Datastr2 = string.Empty,
                Datastr3 = string.Empty,
                Datastr4 = string.Empty,
                Datastr5 = string.Empty,
            };
            globalData.TryAdd(id, result);
            await ServerDbContext.CreateAsync(result);
            return result;
        }

        public static long GetData(DbDynaGlobalData data, int idx)
        {
            switch (idx)
            {
                case 0: return data.Data0;
                case 1: return data.Data1;
                case 2: return data.Data2;
                case 3: return data.Data3;
                case 4: return data.Data4;
                case 5: return data.Data5;
                default: return 0;
            }
        }

        public static void ChangeData(DbDynaGlobalData data, int idx, long value)
        {
            switch (idx)
            {
                case 0: data.Data0 = value; break;
                case 1: data.Data1 = value; break;
                case 2: data.Data2 = value; break;
                case 3: data.Data3 = value; break;
                case 4: data.Data4 = value; break;
                case 5: data.Data5 = value; break;
            }
        }

        public static string GetStringData(DbDynaGlobalData data, int idx)
        {
            switch (idx)
            {
                case 0: return data.Datastr0;
                case 1: return data.Datastr1;
                case 2: return data.Datastr2;
                case 3: return data.Datastr3;
                case 4: return data.Datastr4;
                case 5: return data.Datastr5;
                default: return string.Empty;
            }
        }

        public static void ChangeStringData(DbDynaGlobalData data, int idx, string value)
        {
            switch (idx)
            {
                case 0: data.Datastr0 = value; break;
                case 1: data.Datastr1 = value; break;
                case 2: data.Datastr2 = value; break;
                case 3: data.Datastr3 = value; break;
                case 4: data.Datastr4 = value; break;
                case 5: data.Datastr5 = value; break;
            }
        }

        public static uint GetTime(DbDynaGlobalData data, int idx)
        {
            switch (idx)
            {
                case 0: return data.Time0;
                case 1: return data.Time1;
                case 2: return data.Time2;
                case 3: return data.Time3;
                case 4: return data.Time4;
                case 5: return data.Time5;
                default: return 0;
            }
        }

        public static void ChangeTime(DbDynaGlobalData data, int idx, uint value)
        {
            switch (idx)
            {
                case 0: data.Time0 = value; break;
                case 1: data.Time1 = value; break;
                case 2: data.Time2 = value; break;
                case 3: data.Time3 = value; break;
                case 4: data.Time4 = value; break;
                case 5: data.Time5 = value; break;
            }
        }
    }

    public static class ServerRatesManager
    {
        private static readonly ILogger logger = LogFactory.CreateLogger("ServerRatesManager");

        private static readonly JsonSerializerOptions jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private static volatile ServerConfiguration.ServerRatesConfiguration current = new();
        private static long lastWriteTicks = 0;
        private static long lastCommandsTicks = 0;
        private static long lastBroadcastStamp = 0;
        private static long lastReloadActionsStamp = 0;
        private static DateTime lastReloadTime = DateTime.Now;
        private static int statusTickCounter = 0;
        private static bool shopsSnapshotWritten = false;

        private static readonly HashSet<string> processedCommandIds = new(StringComparer.Ordinal);
        private static readonly LinkedList<StudioCommandResult> recentResults = new();
        private static readonly SemaphoreSlim promoLock = new(1, 1);

        private static long lastPromosTicks = 0;
        private static long lastAutoBroadcastTicks = 0;
        private static long lastCustomDropsTicks = 0;

        private static volatile StudioPromoEnvelope currentPromos = new();
        private static volatile StudioAutoBroadcastEnvelope currentAutoBroadcast = new();
        private static volatile Dictionary<uint, StudioMonsterDropProfile> currentCustomDrops = new();
        private static volatile bool hasCustomMonsterDrops = false;

        private static readonly ConcurrentDictionary<string, DateTime> autoBroadcastLastIntervalUtc = new(StringComparer.Ordinal);
        private static readonly ConcurrentDictionary<string, string> autoBroadcastLastDailyDate = new(StringComparer.Ordinal);

        public static string RatesFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MoonQueen.Rates.json");
        public static string StatusFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MoonQueen.Rates.status.json");
        public static string CommandsFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MoonQueen.Studio.Commands.json");
        public static string ShopsSnapshotFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MoonQueen.Shops.snapshot.json");
        public static string SearchSnapshotFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MoonQueen.Studio.Search.json");
        public static string PromosFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MoonQueen.Studio.Promos.json");
        public static string AutoBroadcastFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MoonQueen.Studio.AutoBroadcast.json");
        public static string CustomDropsFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MoonQueen.Studio.CustomDrops.json");
        public static string InspectSnapshotFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MoonQueen.Studio.Inspect.json");

        public static ServerConfiguration.ServerRatesConfiguration Current => current;
        public static bool HasCustomMonsterDrops => hasCustomMonsterDrops;

        public sealed class StudioCommandEnvelope
        {
            public List<StudioCommandItem> Commands { get; set; } = new();
        }

        public sealed class StudioCommandItem
        {
            public string Id { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;

            // Tool #1: Events
            public string EventKey { get; set; } = string.Empty;
            public string Action { get; set; } = string.Empty;
            public int DurationMinutes { get; set; } = 60;
            public bool Broadcast { get; set; } = true;

            // Tool #2: Shops & Item Prices
            public uint NpcId { get; set; }
            public uint GoodsId { get; set; }
            public uint ItemType { get; set; }
            public uint MoneyType { get; set; }
            public long SilverPrice { get; set; } = -1;
            public long CpPrice { get; set; } = -1;
            public long BoundCpPrice { get; set; } = -1;
            public long HonorPrice { get; set; } = -1;
            public long RidingPrice { get; set; } = -1;

            // Tool #3: Online Players & Rewards
            public uint PlayerId { get; set; }
            public string PlayerName { get; set; } = string.Empty;
            public long SilverAmount { get; set; }
            public int CpAmount { get; set; }
            public int BoundCpAmount { get; set; }
            public int BlessingHours { get; set; }
            public int VipLevel { get; set; } = -1;
            public int ItemAmount { get; set; } = 1;
            public byte Plus { get; set; }
            public byte Bless { get; set; }
            public byte Enchant { get; set; }
            public byte SocketOne { get; set; }
            public byte SocketTwo { get; set; }
            public bool Bound { get; set; }
            public string Message { get; set; } = string.Empty;

            // Tool #3 & #4: Map / Boss Spawner / Search
            public uint MapId { get; set; }
            public ushort X { get; set; }
            public ushort Y { get; set; }
            public uint MonsterType { get; set; }
            public int Count { get; set; } = 1;
            public int ScatterRadius { get; set; } = 3;
            public uint RoleId { get; set; }
            public string Query { get; set; } = string.Empty;

            // Tool #5, #7, #8: Accounts, Live Inspector & Broadcast Channel
            public uint AccountId { get; set; }
            public ushort AuthorityId { get; set; } = 1;
            public string IpAddress { get; set; } = string.Empty;
            public string MacAddress { get; set; } = string.Empty;
            public uint ItemId { get; set; }
            public string Channel { get; set; } = "TopLeft";
        }

        public sealed class StudioPromoEnvelope
        {
            public List<StudioPromoCode> Codes { get; set; } = new();
        }

        public sealed class StudioPromoCode
        {
            public string Code { get; set; } = string.Empty;
            public string Title { get; set; } = string.Empty;
            public bool Enabled { get; set; } = true;
            public int MaxUses { get; set; } = 0;
            public int UsedCount { get; set; } = 0;
            public int MinLevel { get; set; } = 1;
            public long ExpiresUnix { get; set; } = 0;
            public string ExpiresAt { get; set; } = string.Empty;
            public bool OnePerIpOrMac { get; set; } = false;
            public long SilverAmount { get; set; }
            public int CpAmount { get; set; }
            public int BoundCpAmount { get; set; }
            public int BlessingHours { get; set; }
            public int VipLevel { get; set; } = -1;
            public uint ItemType { get; set; }
            public int ItemAmount { get; set; } = 1;
            public byte Plus { get; set; }
            public byte Bless { get; set; }
            public byte Enchant { get; set; }
            public byte SocketOne { get; set; }
            public byte SocketTwo { get; set; }
            public bool Bound { get; set; } = true;
            public List<uint> RedeemedAccounts { get; set; } = new();
            public List<uint> RedeemedPlayers { get; set; } = new();
            public List<string> RedeemedIps { get; set; } = new();
            public List<string> RedeemedMacs { get; set; } = new();
            public List<StudioPromoRedemptionLog> RecentClaims { get; set; } = new();
        }

        public sealed class StudioPromoRedemptionLog
        {
            public uint AccountId { get; set; }
            public uint PlayerId { get; set; }
            public string PlayerName { get; set; } = string.Empty;
            public string Ip { get; set; } = string.Empty;
            public string ClaimedAt { get; set; } = string.Empty;
        }

        public sealed class StudioAutoBroadcastEnvelope
        {
            public bool Enabled { get; set; } = false;
            public List<StudioAutoBroadcastItem> Items { get; set; } = new();
        }

        public sealed class StudioAutoBroadcastItem
        {
            public string Id { get; set; } = string.Empty;
            public bool Enabled { get; set; } = true;
            public string Mode { get; set; } = "interval"; // "interval" | "daily"
            public int IntervalMinutes { get; set; } = 15;
            public string DailyTime { get; set; } = string.Empty; // "HH:mm"
            public string Channel { get; set; } = "TopLeft"; // TopLeft | Center | Talk | Broadcast
            public string Message { get; set; } = string.Empty;
        }

        public sealed class StudioCustomDropsEnvelope
        {
            public List<StudioMonsterDropProfile> Profiles { get; set; } = new();
        }

        public sealed class StudioMonsterDropProfile
        {
            public uint MonsterType { get; set; }
            public string MonsterName { get; set; } = string.Empty;
            public bool Enabled { get; set; } = true;
            public string Mode { get; set; } = "additional"; // "additional" | "override"
            public List<StudioMonsterDropRule> Rules { get; set; } = new();
        }

        public sealed class StudioMonsterDropRule
        {
            public string Id { get; set; } = string.Empty;
            public uint ItemType { get; set; }
            public string ItemName { get; set; } = string.Empty;
            public double ChancePercent { get; set; } = 10.0;
            public int MinCount { get; set; } = 1;
            public int MaxCount { get; set; } = 1;
            public byte Plus { get; set; }
            public byte Bless { get; set; }
            public byte Sockets { get; set; }
            public bool Bound { get; set; }
            public long BonusSilver { get; set; }
            public int BonusCps { get; set; }
            public int BonusBoundCps { get; set; }
            public bool WorldAnnounce { get; set; }
        }

        public sealed class StudioCommandResult
        {
            public string id { get; set; } = string.Empty;
            public string type { get; set; } = string.Empty;
            public bool ok { get; set; }
            public string message { get; set; } = string.Empty;
            public string executed_at { get; set; } = string.Empty;
        }

        public static async Task InitializeAsync()
        {
            DeveloperRightsGuard.VerifyCoreIntegrity();

            var initial = (ServerConfiguration.Configuration?.Rates ?? new ServerConfiguration.ServerRatesConfiguration()).CloneNormalized();
            current = initial;

            try
            {
                if (!File.Exists(RatesFilePath))
                {
                    string seedJson = JsonSerializer.Serialize(initial, jsonOptions);
                    await File.WriteAllTextAsync(RatesFilePath, seedJson);
                    lastWriteTicks = File.GetLastWriteTimeUtc(RatesFilePath).Ticks;
                }
                else
                {
                    await ReloadFromDiskAsync(processTriggers: false);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[MoonQueen Studio] Could not initialize MoonQueen.Rates.json, using in-memory defaults.");
            }

            await ReloadPromosIfChangedAsync(force: true);
            await ReloadAutoBroadcastIfChangedAsync(force: true);
            await ReloadCustomDropsIfChangedAsync(force: true);
            await WriteStatusSnapshotAsync("Initialized");
        }

        public static async Task OnTimerAsync()
        {
            try
            {
                if (!shopsSnapshotWritten && RoleManager.RolesCount > 0)
                {
                    shopsSnapshotWritten = true;
                    await WriteShopsSnapshotAsync();
                }

                if (File.Exists(CommandsFilePath))
                {
                    long cmdTicks = File.GetLastWriteTimeUtc(CommandsFilePath).Ticks;
                    if (cmdTicks != 0 && cmdTicks != lastCommandsTicks)
                    {
                        await ProcessStudioCommandsAsync();
                    }
                }

                await ReloadPromosIfChangedAsync(force: false);
                await ReloadAutoBroadcastIfChangedAsync(force: false);
                await ReloadCustomDropsIfChangedAsync(force: false);
                await ProcessAutoBroadcastTickAsync();

                if (File.Exists(RatesFilePath))
                {
                    long diskTicks = File.GetLastWriteTimeUtc(RatesFilePath).Ticks;
                    if (diskTicks != 0 && diskTicks != lastWriteTicks)
                    {
                        await ReloadFromDiskAsync(processTriggers: true);
                        return;
                    }
                }

                statusTickCounter++;
                if (statusTickCounter >= 3)
                {
                    statusTickCounter = 0;
                    await WriteStatusSnapshotAsync("Heartbeat");
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[MoonQueen Studio] Auto-reload check warning.");
            }
        }

        private static void RecordResult(string id, string type, bool ok, string message)
        {
            lock (recentResults)
            {
                recentResults.AddFirst(new StudioCommandResult
                {
                    id = id ?? string.Empty,
                    type = type ?? string.Empty,
                    ok = ok,
                    message = message ?? string.Empty,
                    executed_at = DateTime.Now.ToString("HH:mm:ss")
                });
                while (recentResults.Count > 35)
                {
                    recentResults.RemoveLast();
                }
            }
        }

        private static async Task ProcessStudioCommandsAsync()
        {
            StudioCommandEnvelope env;
            try
            {
                string raw = await File.ReadAllTextAsync(CommandsFilePath);
                if (string.IsNullOrWhiteSpace(raw))
                {
                    lastCommandsTicks = File.GetLastWriteTimeUtc(CommandsFilePath).Ticks;
                    return;
                }
                env = JsonSerializer.Deserialize<StudioCommandEnvelope>(raw, jsonOptions);
            }
            catch (IOException)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[MoonQueen Studio] Invalid MoonQueen.Studio.Commands.json payload.");
                lastCommandsTicks = File.GetLastWriteTimeUtc(CommandsFilePath).Ticks;
                return;
            }

            if (env?.Commands == null || env.Commands.Count == 0)
            {
                lastCommandsTicks = File.GetLastWriteTimeUtc(CommandsFilePath).Ticks;
                return;
            }

            bool anyExecuted = false;
            foreach (var cmd in env.Commands)
            {
                if (cmd == null || string.IsNullOrWhiteSpace(cmd.Type))
                {
                    continue;
                }

                string cmdId = string.IsNullOrWhiteSpace(cmd.Id) ? Guid.NewGuid().ToString("N") : cmd.Id;
                if (!processedCommandIds.Add(cmdId))
                {
                    continue;
                }

                if (processedCommandIds.Count > 500)
                {
                    processedCommandIds.Clear();
                    processedCommandIds.Add(cmdId);
                }

                anyExecuted = true;
                try
                {
                    var (ok, msg) = await ExecuteSingleStudioCommandAsync(cmd);
                    RecordResult(cmdId, cmd.Type, ok, msg);
                    logger.LogInformation("[MoonQueen Studio] Command [{Type}] ({Id}) -> Ok={Ok}: {Msg}", cmd.Type, cmdId, ok, msg);
                }
                catch (Exception ex)
                {
                    RecordResult(cmdId, cmd.Type, false, ex.Message);
                    logger.LogError(ex, "[MoonQueen Studio] Error executing command [{Type}] ({Id})", cmd.Type, cmdId);
                }
            }

            try
            {
                string cleared = JsonSerializer.Serialize(new StudioCommandEnvelope(), jsonOptions);
                string tmp = CommandsFilePath + ".tmp";
                await File.WriteAllTextAsync(tmp, cleared);
                File.Move(tmp, CommandsFilePath, overwrite: true);
                lastCommandsTicks = File.GetLastWriteTimeUtc(CommandsFilePath).Ticks;
            }
            catch
            {
                lastCommandsTicks = File.GetLastWriteTimeUtc(CommandsFilePath).Ticks;
            }

            if (anyExecuted)
            {
                await WriteStatusSnapshotAsync("CommandExecuted");
            }
        }

        private static async Task<(bool Ok, string Message)> ExecuteSingleStudioCommandAsync(StudioCommandItem cmd)
        {
            string type = cmd.Type.Trim().ToLowerInvariant();
            switch (type)
            {
                // =========================================================
                // TOOL #1: LIVE EVENTS & TOURNAMENTS STUDIO
                // =========================================================
                case "event_control":
                    return await EventManager.TriggerStudioEventAsync(cmd.EventKey, cmd.Action, cmd.DurationMinutes, cmd.Broadcast);

                // =========================================================
                // TOOL #2: LIVE SHOP & MALL HOT-EDITOR
                // =========================================================
                case "shop_reload":
                {
                    var (items, shops) = await ItemManager.ReloadShopsAndPricesAsync();
                    await WriteShopsSnapshotAsync();
                    return (true, $"Hot-reloaded {items} itemtypes & {shops} shops in memory (<1s).");
                }

                case "shop_upsert":
                {
                    if (cmd.NpcId == 0 || cmd.ItemType == 0)
                    {
                        return (false, "NpcId and ItemType are required.");
                    }

                    DbItemtype it = ItemManager.GetItemtype(cmd.ItemType);
                    if (it == null)
                    {
                        return (false, $"ItemType #{cmd.ItemType} does not exist in cq_itemtype.");
                    }

                    await using var db = new ServerDbContext();
                    var dbItem = await db.Itemtypes.FirstOrDefaultAsync(x => x.Type == cmd.ItemType);
                    if (dbItem != null)
                    {
                        if (cmd.SilverPrice >= 0) dbItem.Price = (uint)Math.Min(uint.MaxValue, cmd.SilverPrice);
                        if (cmd.CpPrice >= 0) dbItem.EmoneyPrice = (uint)Math.Min(uint.MaxValue, cmd.CpPrice);
                        if (cmd.BoundCpPrice >= 0) dbItem.BoundEmoneyPrice = (uint)Math.Min(uint.MaxValue, cmd.BoundCpPrice);
                        db.Itemtypes.Update(dbItem);
                        ItemManager.UpsertItemtypeInMemory(dbItem);
                    }

                    var existingGood = await db.Goods.FirstOrDefaultAsync(x =>
                        (cmd.GoodsId > 0 && x.Identity == cmd.GoodsId)
                        || (x.OwnerIdentity == cmd.NpcId && x.Itemtype == cmd.ItemType && x.Moneytype == cmd.MoneyType));

                    if (existingGood == null)
                    {
                        existingGood = new DbGoods
                        {
                            OwnerIdentity = cmd.NpcId,
                            Itemtype = cmd.ItemType,
                            Moneytype = cmd.MoneyType,
                            HonorPrice = cmd.HonorPrice >= 0 ? (uint)cmd.HonorPrice : 0u,
                            RidingPrice = cmd.RidingPrice >= 0 ? (uint)cmd.RidingPrice : 0u,
                            GoldenLeaguePrice = 0u,
                            CompetePlace = 0
                        };
                        db.Goods.Add(existingGood);
                    }
                    else
                    {
                        existingGood.OwnerIdentity = cmd.NpcId;
                        existingGood.Itemtype = cmd.ItemType;
                        existingGood.Moneytype = cmd.MoneyType;
                        if (cmd.HonorPrice >= 0) existingGood.HonorPrice = (uint)cmd.HonorPrice;
                        if (cmd.RidingPrice >= 0) existingGood.RidingPrice = (uint)cmd.RidingPrice;
                        db.Goods.Update(existingGood);
                    }

                    await db.SaveChangesAsync();
                    await ItemManager.ReloadShopsAndPricesAsync();
                    await WriteShopsSnapshotAsync();
                    return (true, $"Saved [{it.Name} #{cmd.ItemType}] in Shop #{cmd.NpcId} and hot-reloaded live.");
                }

                case "shop_delete":
                {
                    await using var db = new ServerDbContext();
                    DbGoods target = null;
                    if (cmd.GoodsId > 0)
                    {
                        target = await db.Goods.FirstOrDefaultAsync(x => x.Identity == cmd.GoodsId);
                    }
                    else if (cmd.NpcId > 0 && cmd.ItemType > 0)
                    {
                        target = await db.Goods.FirstOrDefaultAsync(x => x.OwnerIdentity == cmd.NpcId && x.Itemtype == cmd.ItemType);
                    }

                    if (target == null)
                    {
                        return (false, "Shop item entry not found in cq_goods.");
                    }

                    db.Goods.Remove(target);
                    await db.SaveChangesAsync();
                    await ItemManager.ReloadShopsAndPricesAsync();
                    await WriteShopsSnapshotAsync();
                    return (true, $"Removed Item #{target.Itemtype} from Shop #{target.OwnerIdentity} live.");
                }

                case "item_update_price":
                {
                    if (cmd.ItemType == 0)
                    {
                        return (false, "ItemType is required.");
                    }

                    await using var db = new ServerDbContext();
                    var dbItem = await db.Itemtypes.FirstOrDefaultAsync(x => x.Type == cmd.ItemType);
                    if (dbItem == null)
                    {
                        return (false, $"ItemType #{cmd.ItemType} not found in cq_itemtype.");
                    }

                    if (cmd.SilverPrice >= 0) dbItem.Price = (uint)Math.Min(uint.MaxValue, cmd.SilverPrice);
                    if (cmd.CpPrice >= 0) dbItem.EmoneyPrice = (uint)Math.Min(uint.MaxValue, cmd.CpPrice);
                    if (cmd.BoundCpPrice >= 0) dbItem.BoundEmoneyPrice = (uint)Math.Min(uint.MaxValue, cmd.BoundCpPrice);
                    db.Itemtypes.Update(dbItem);
                    await db.SaveChangesAsync();

                    ItemManager.UpsertItemtypeInMemory(dbItem);
                    await WriteShopsSnapshotAsync();
                    return (true, $"Updated prices for [{dbItem.Name} #{dbItem.Type}] (Silver={dbItem.Price}, CPs={dbItem.EmoneyPrice}, BoundCPs={dbItem.BoundEmoneyPrice}).");
                }

                case "catalog_search":
                {
                    string q = (cmd.Query ?? string.Empty).Trim();
                    uint.TryParse(q, out uint exactId);

                    var items = ItemManager.QueryItemtypes(it =>
                            string.IsNullOrEmpty(q)
                            || it.Type == exactId
                            || (it.Name != null && it.Name.Contains(q, StringComparison.OrdinalIgnoreCase)))
                        .OrderBy(x => x.Type)
                        .Take(80)
                        .Select(it => new
                        {
                            id = it.Type,
                            name = it.Name,
                            req_level = it.ReqLevel,
                            silver = it.Price,
                            cps = it.EmoneyPrice,
                            bound_cps = it.BoundEmoneyPrice
                        })
                        .ToList();

                    var monsters = RoleManager.QueryMonsterTypes(m =>
                            string.IsNullOrEmpty(q)
                            || m.Id == exactId
                            || (m.Name != null && m.Name.Contains(q, StringComparison.OrdinalIgnoreCase)))
                        .OrderByDescending(x => x.Life)
                        .Take(80)
                        .Select(m => new
                        {
                            id = m.Id,
                            name = m.Name,
                            level = m.Level,
                            life = m.Life,
                            attack_max = m.AttackMax
                        })
                        .ToList();

                    var payload = new
                    {
                        query = q,
                        generated_at = DateTime.Now.ToString("HH:mm:ss"),
                        items,
                        monsters
                    };
                    string tmp = SearchSnapshotFilePath + ".tmp";
                    await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(payload, jsonOptions));
                    File.Move(tmp, SearchSnapshotFilePath, overwrite: true);
                    return (true, $"Catalog search '{q}': {items.Count} items, {monsters.Count} monsters.");
                }

                // =========================================================
                // TOOL #3: LIVE ONLINE PLAYERS RADAR & GIFT / ACTION BOX
                // =========================================================
                case "player_reward":
                {
                    Character target = cmd.PlayerId > 0
                        ? RoleManager.GetUser(cmd.PlayerId)
                        : RoleManager.GetUser(cmd.PlayerName ?? string.Empty);

                    if (target == null)
                    {
                        return (false, $"Player '{(cmd.PlayerId > 0 ? cmd.PlayerId.ToString() : cmd.PlayerName)}' is not currently online.");
                    }

                    string summary = await ApplyRewardPackageToUserAsync(target, cmd);
                    return (true, $"Delivered to [{target.Name}]: {summary}");
                }

                case "reward_all_online":
                {
                    var users = RoleManager.QueryUserSet();
                    if (users.Count == 0)
                    {
                        return (false, "No players are currently online.");
                    }

                    int rewarded = 0;
                    string sampleSummary = string.Empty;
                    foreach (var user in users)
                    {
                        sampleSummary = await ApplyRewardPackageToUserAsync(user, cmd);
                        rewarded++;
                    }

                    if (cmd.Broadcast)
                    {
                        string worldMsg = !string.IsNullOrWhiteSpace(cmd.Message)
                            ? cmd.Message
                            : $"[{DeveloperRightsGuard.BrandName}] All {rewarded} online heroes received an instant Admin Gift ({sampleSummary})!";
                        await BroadcastWorldMsgAsync(worldMsg, TalkChannel.Center, Color.Gold);
                    }

                    return (true, $"Delivered global gift to {rewarded} online player(s) ({sampleSummary}).");
                }

                case "player_action":
                {
                    Character target = cmd.PlayerId > 0
                        ? RoleManager.GetUser(cmd.PlayerId)
                        : RoleManager.GetUser(cmd.PlayerName ?? string.Empty);

                    if (target == null)
                    {
                        return (false, "Target player is not online.");
                    }

                    string act = (cmd.Action ?? string.Empty).Trim().ToLowerInvariant();
                    switch (act)
                    {
                        case "teleport":
                        {
                            uint mapId = cmd.MapId > 0 ? cmd.MapId : 1002u;
                            ushort x = cmd.X > 0 ? cmd.X : (ushort)300;
                            ushort y = cmd.Y > 0 ? cmd.Y : (ushort)278;
                            GameMap dest = MapManager.GetMap(mapId);
                            if (dest == null)
                            {
                                return (false, $"Map #{mapId} does not exist.");
                            }
                            await target.FlyMapAsync(mapId, x, y);
                            await target.SendAsync($"[{DeveloperRightsGuard.BrandName}] You have been teleported to {dest.Name} ({x}, {y}).", TalkChannel.System, Color.Gold);
                            return (true, $"Teleported [{target.Name}] to {dest.Name} #{mapId} ({x}, {y}).");
                        }

                        case "heal_revive":
                        {
                            if (!target.IsAlive)
                            {
                                await target.RebornAsync(false, true);
                            }
                            await target.SetAttributesAsync(ClientUpdateType.Hitpoints, target.MaxLife);
                            await target.SetAttributesAsync(ClientUpdateType.Mana, target.MaxMana);
                            await target.SetAttributesAsync(ClientUpdateType.Stamina, (ulong)Role.DEFAULT_USER_ENERGY);
                            await target.SendEffectAsync("angelwing", true);
                            await target.SendAsync($"[{DeveloperRightsGuard.BrandName}] You have been fully revived & healed by Administration!", TalkChannel.System, Color.Gold);
                            return (true, $"Revived & restored full HP/MP/Stamina for [{target.Name}].");
                        }

                        case "clear_pk":
                        {
                            await target.SetAttributesAsync(ClientUpdateType.PkPoints, 0);
                            await target.DetachStatusAsync(StatusSet.RED_NAME);
                            await target.DetachStatusAsync(StatusSet.BLACK_NAME);
                            await target.SendAsync($"[{DeveloperRightsGuard.BrandName}] Your PK Points have been cleared to 0!", TalkChannel.System, Color.Gold);
                            return (true, $"Cleared PK Points to 0 for [{target.Name}].");
                        }

                        case "jail":
                        {
                            await target.FlyMapAsync(6002, 28, 74);
                            await target.SendAsync($"[{DeveloperRightsGuard.BrandName}] You have been moved to BotJail by Administration.", TalkChannel.System, Color.Red);
                            return (true, $"Sent [{target.Name}] to BotJail (6002).");
                        }

                        case "unjail":
                        {
                            await target.FlyMapAsync(1002, 300, 278);
                            await target.SendAsync($"[{DeveloperRightsGuard.BrandName}] You have been released to Twin City!", TalkChannel.System, Color.Gold);
                            return (true, $"Released [{target.Name}] to Twin City (1002, 300, 278).");
                        }

                        case "kick":
                        {
                            string name = target.Name;
                            await RoleManager.KickOutAsync(target.Identity, "Disconnected by MoonQueen Studio");
                            return (true, $"Kicked [{name}] from the server.");
                        }

                        default:
                            return (false, $"Unsupported player action: {cmd.Action}");
                    }
                }

                // =========================================================
                // TOOL #4: LIVE BOSS & INVASION SPAWNER STUDIO
                // =========================================================
                case "boss_spawn":
                {
                    uint mapId = cmd.MapId > 0 ? cmd.MapId : 1002u;
                    GameMap map = MapManager.GetMap(mapId);
                    if (map == null)
                    {
                        return (false, $"Map #{mapId} does not exist.");
                    }

                    DbMonstertype mobType = RoleManager.GetMonstertype(cmd.MonsterType);
                    if (mobType == null)
                    {
                        return (false, $"MonsterType #{cmd.MonsterType} was not found in cq_monstertype.");
                    }

                    int spawnCount = Math.Clamp(cmd.Count <= 0 ? 1 : cmd.Count, 1, 25);
                    int radius = Math.Clamp(cmd.ScatterRadius, 0, 25);
                    ushort baseX = cmd.X > 0 ? cmd.X : (ushort)300;
                    ushort baseY = cmd.Y > 0 ? cmd.Y : (ushort)278;

                    var msg = new MsgAiSpawnNpc
                    {
                        Mode = AiSpawnNpcMode.Spawn
                    };

                    var rng = Random.Shared;
                    for (int i = 0; i < spawnCount; i++)
                    {
                        ushort sx = baseX;
                        ushort sy = baseY;
                        if (spawnCount > 1 && radius > 0)
                        {
                            sx = (ushort)Math.Clamp(baseX + rng.Next(-radius, radius + 1), 1, Math.Max(1, map.Width - 2));
                            sy = (ushort)Math.Clamp(baseY + rng.Next(-radius, radius + 1), 1, Math.Max(1, map.Height - 2));
                        }

                        msg.List.Add(new MsgAiSpawnNpc<AiClient>.SpawnNpc
                        {
                            GeneratorId = 0,
                            MapId = mapId,
                            MonsterType = cmd.MonsterType,
                            OwnerId = 0,
                            X = sx,
                            Y = sy,
                            OwnerType = 0,
                            Data = 0
                        });
                    }

                    await BroadcastNpcMsgAsync(msg);

                    if (cmd.Broadcast)
                    {
                        string announce = !string.IsNullOrWhiteSpace(cmd.Message)
                            ? cmd.Message
                            : spawnCount > 1
                                ? $"[{DeveloperRightsGuard.BrandName} Invasion] {spawnCount}x {mobType.Name} (Lv.{mobType.Level}) have invaded {map.Name} around ({baseX}, {baseY})! Rally to hunt them!"
                                : $"[{DeveloperRightsGuard.BrandName} Boss Alert] {mobType.Name} (Lv.{mobType.Level}) has appeared in {map.Name} at ({baseX}, {baseY})!";
                        await BroadcastWorldMsgAsync(announce, TalkChannel.Center, Color.Gold);
                    }

                    return (true, $"Spawned {spawnCount}x [{mobType.Name} #{mobType.Id}] on {map.Name} ({baseX}, {baseY}) via MoonQueen.Ai.");
                }

                case "boss_clear":
                {
                    var targets = RoleManager.QueryRoles(r =>
                        r is Monster m
                        && !m.HasGenerator
                        && (cmd.RoleId == 0 || m.Identity == cmd.RoleId)
                        && (cmd.MapId == 0 || m.MapIdentity == cmd.MapId))
                        .Cast<Monster>()
                        .ToList();

                    int removed = 0;
                    foreach (var mob in targets)
                    {
                        await mob.LeaveMapAsync();
                        RoleManager.RemoveRole(mob.Identity);
                        removed++;
                    }

                    return (true, $"Cleared {removed} Studio-spawned boss/invasion monster(s).");
                }

                // =========================================================
                // TOOL #5: ACCOUNTS, BAN KICK & LIVE AUTHORITY SYNC
                // =========================================================
                case "account_kick":
                {
                    string matchIp = (cmd.IpAddress ?? string.Empty).Trim();
                    string matchMac = (cmd.MacAddress ?? string.Empty).Trim();

                    var victims = RoleManager.QueryUserSet()
                        .Where(u =>
                            (cmd.AccountId > 0 && u.Client?.AccountIdentity == cmd.AccountId)
                            || (cmd.PlayerId > 0 && u.Identity == cmd.PlayerId)
                            || (!string.IsNullOrEmpty(matchIp) && string.Equals(u.Client?.IpAddress, matchIp, StringComparison.OrdinalIgnoreCase))
                            || (!string.IsNullOrEmpty(matchMac) && string.Equals(u.Client?.MacAddress, matchMac, StringComparison.OrdinalIgnoreCase)))
                        .ToList();

                    int kicked = 0;
                    foreach (var v in victims)
                    {
                        if (!string.IsNullOrWhiteSpace(cmd.Message))
                        {
                            await v.SendAsync(cmd.Message, TalkChannel.Talk, Color.Red);
                        }
                        await RoleManager.KickOutAsync(v.Identity, string.IsNullOrWhiteSpace(cmd.Message) ? "Account action from MoonQueen Control Studio" : cmd.Message);
                        kicked++;
                    }
                    return (true, kicked > 0 ? $"Disconnected {kicked} online session(s) immediately." : "Account updated (player was not currently online).");
                }

                case "account_sync_authority":
                {
                    var targets = RoleManager.QueryUserSet()
                        .Where(u =>
                            (cmd.AccountId > 0 && u.Client?.AccountIdentity == cmd.AccountId)
                            || (cmd.PlayerId > 0 && u.Identity == cmd.PlayerId))
                        .ToList();

                    foreach (var t in targets)
                    {
                        if (t.Client != null)
                        {
                            t.Client.AuthorityLevel = cmd.AuthorityId;
                            await t.LoadTitlesAsync();
                        }
                    }
                    return (true, targets.Count > 0
                        ? $"Synced live AuthorityLevel={cmd.AuthorityId} for {targets.Count} online character(s)."
                        : $"AuthorityLevel={cmd.AuthorityId} saved (will apply on next login).");
                }

                // =========================================================
                // TOOL #7: LIVE INVENTORY & WAREHOUSE INSPECTOR
                // =========================================================
                case "player_inspect":
                {
                    Character target = cmd.PlayerId > 0
                        ? RoleManager.GetUser(cmd.PlayerId)
                        : RoleManager.GetUser(cmd.PlayerName ?? string.Empty);

                    if (target == null)
                    {
                        return (false, "Player is currently offline (Studio reads offline inventory directly from database).");
                    }

                    await target.SaveAsync();
                    var liveItems = target.UserPackage.GetAllItemsForStudio()
                        .Select(it => new
                        {
                            id = it.Identity,
                            type = it.Type,
                            name = it.Name,
                            position = (ushort)it.Position,
                            owner_id = it.OwnerIdentity,
                            plus = it.Plus,
                            bless = it.ReduceDamage,
                            enchant = it.Enchantment,
                            gem1 = (byte)it.SocketOne,
                            gem2 = (byte)it.SocketTwo,
                            durability = it.Durability,
                            max_durability = it.MaximumDurability,
                            stack = it.AccumulateNum,
                            bound = it.IsBound
                        })
                        .ToList();

                    var payload = new
                    {
                        online = true,
                        generated_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        generated_unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                        player = new
                        {
                            id = target.Identity,
                            account_id = target.Client?.AccountIdentity ?? 0,
                            name = target.Name,
                            level = target.Level,
                            reborn = target.Metempsychosis,
                            profession = target.Profession,
                            bp = target.BattlePower,
                            silvers = target.Silvers,
                            storage_money = target.StorageMoney,
                            cps = target.ConquerPoints,
                            bound_cps = target.ConquerPointsBound,
                            pk = target.PkPoints,
                            vip = target.BaseVipLevel,
                            map_id = target.MapIdentity,
                            map_name = target.Map?.Name ?? $"Map #{target.MapIdentity}",
                            x = target.X,
                            y = target.Y
                        },
                        items = liveItems
                    };

                    string tmp = InspectSnapshotFilePath + ".tmp";
                    await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(payload, jsonOptions));
                    File.Move(tmp, InspectSnapshotFilePath, overwrite: true);
                    return (true, $"Exported live package snapshot for [{target.Name}] ({liveItems.Count} items).");
                }

                case "player_delete_item":
                {
                    if (cmd.ItemId == 0)
                    {
                        return (false, "Invalid ItemId (0).");
                    }

                    Character target = cmd.PlayerId > 0
                        ? RoleManager.GetUser(cmd.PlayerId)
                        : RoleManager.GetUser(cmd.PlayerName ?? string.Empty);

                    if (target == null)
                    {
                        return (false, "Player is offline; delete handled directly via database.");
                    }

                    var (removed, itemName) = await target.UserPackage.StudioDeleteItemByIdAsync(cmd.ItemId);
                    if (!removed)
                    {
                        return (false, $"Item #{cmd.ItemId} was not found in [{target.Name}]'s live package.");
                    }

                    await target.SaveAsync();
                    return (true, $"Removed item [{itemName} #{cmd.ItemId}] live from [{target.Name}].");
                }

                // =========================================================
                // TOOL #8: INSTANT BROADCAST PREVIEW
                // =========================================================
                case "broadcast_now":
                {
                    if (string.IsNullOrWhiteSpace(cmd.Message))
                    {
                        return (false, "Broadcast message cannot be empty.");
                    }

                    TalkChannel ch = ResolveTalkChannel(cmd.Channel);
                    await BroadcastWorldMsgAsync(cmd.Message.Trim(), ch, Color.Gold);
                    return (true, $"Broadcasted message on [{ch}]: {cmd.Message}");
                }

                default:
                    return (false, $"Unknown studio command type: {cmd.Type}");
            }
        }

        private static async Task<string> ApplyRewardPackageToUserAsync(Character target, StudioCommandItem cmd)
        {
            var parts = new List<string>();

            if (cmd.SilverAmount > 0)
            {
                await target.AwardMoneyAsync(cmd.SilverAmount);
                parts.Add($"+{cmd.SilverAmount:N0} Silver");
            }

            if (cmd.CpAmount > 0)
            {
                await target.ChangeConquerPointsAsync(cmd.CpAmount, true);
                parts.Add($"+{cmd.CpAmount:N0} CPs");
            }

            if (cmd.BoundCpAmount > 0)
            {
                await target.ChangeBoundConquerPointsAsync(cmd.BoundCpAmount, true);
                parts.Add($"+{cmd.BoundCpAmount:N0} Bound CPs");
            }

            if (cmd.BlessingHours > 0)
            {
                await target.AddBlessingAsync((uint)cmd.BlessingHours);
                parts.Add($"+{cmd.BlessingHours}h Heaven Blessing");
            }

            if (cmd.VipLevel is >= 0 and <= 6)
            {
                await target.SetAttributesAsync(ClientUpdateType.VipLevel, (ulong)cmd.VipLevel);
                parts.Add($"VIP {cmd.VipLevel}");
            }

            if (cmd.ItemType > 0)
            {
                DbItemtype itemtype = ItemManager.GetItemtype(cmd.ItemType);
                if (itemtype != null)
                {
                    int qty = Math.Clamp(cmd.ItemAmount <= 0 ? 1 : cmd.ItemAmount, 1, 20);
                    int delivered = 0;
                    byte plus = (byte)Math.Clamp((int)cmd.Plus, 0, 12);
                    byte bless = (byte)Math.Clamp((int)cmd.Bless, 0, 7);
                    byte enchant = cmd.Enchant;

                    for (int i = 0; i < qty; i++)
                    {
                        if (!target.UserPackage.IsPackSpare(1, itemtype.Type))
                        {
                            break;
                        }

                        Item item = new(target);
                        if (!await item.CreateAsync(itemtype, Item.ItemPosition.Inventory, cmd.Bound))
                        {
                            break;
                        }

                        if (item.IsActivable())
                        {
                            await item.ActivateAsync();
                        }

                        if (item.IsEquipment())
                        {
                            if (plus > 0) item.ChangeAddition(plus);
                            if (bless > 0) item.ReduceDamage = bless;
                            if (enchant > 0) item.Enchantment = enchant;
                            if (cmd.SocketOne > 0) item.SocketOne = (Item.SocketGem)cmd.SocketOne;
                            if (cmd.SocketTwo > 0) item.SocketTwo = (Item.SocketGem)cmd.SocketTwo;
                        }
                        else if (item.IsMount() && plus > 0)
                        {
                            item.ChangeAddition(plus);
                        }

                        if (await target.UserPackage.AddItemAsync(item, true))
                        {
                            await item.SaveAsync();
                            delivered++;
                        }
                    }

                    if (delivered > 0)
                    {
                        string suffix = plus > 0 ? $" (+{plus})" : string.Empty;
                        parts.Add($"{delivered}x {itemtype.Name}{suffix}");
                    }
                }
            }

            await target.SaveAsync();
            await target.SendEffectAsync("angelwing", true);

            string summary = parts.Count > 0 ? string.Join(", ", parts) : "Gift Package";
            string personalMsg = !string.IsNullOrWhiteSpace(cmd.Message)
                ? cmd.Message
                : $"[{DeveloperRightsGuard.BrandName} Gift] You received: {summary}!";
            await target.SendAsync(personalMsg, TalkChannel.System, Color.Gold);
            return summary;
        }

        private static async Task WriteShopsSnapshotAsync()
        {
            try
            {
                var snap = ItemManager.BuildShopsSnapshot();
                string tmp = ShopsSnapshotFilePath + ".tmp";
                await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(snap, jsonOptions));
                File.Move(tmp, ShopsSnapshotFilePath, overwrite: true);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[MoonQueen Studio] Could not write MoonQueen.Shops.snapshot.json.");
            }
        }

        public static async Task<bool> ReloadFromDiskAsync(bool processTriggers = true)
        {
            try
            {
                if (!File.Exists(RatesFilePath))
                {
                    var fallback = current.CloneNormalized();
                    await File.WriteAllTextAsync(RatesFilePath, JsonSerializer.Serialize(fallback, jsonOptions));
                    lastWriteTicks = File.GetLastWriteTimeUtc(RatesFilePath).Ticks;
                    await WriteStatusSnapshotAsync("Recreated default file");
                    return true;
                }

                string raw = await File.ReadAllTextAsync(RatesFilePath);
                var parsed = JsonSerializer.Deserialize<ServerConfiguration.ServerRatesConfiguration>(raw, jsonOptions);
                if (parsed == null)
                {
                    return false;
                }

                var normalized = parsed.CloneNormalized();
                current = normalized;
                lastWriteTicks = File.GetLastWriteTimeUtc(RatesFilePath).Ticks;
                lastReloadTime = DateTime.Now;

                logger.LogInformation(
                    "[MoonQueen Studio] Live rates active [{Preset}] -> EXP:{Exp}x | Magic:{Mag}x | Weapon:{Wep}x | Silver:{Sil}x ({SilCh}%) | Equip:{Eq}x | Quality:{Qual}x | Soc:{Soc}x | +1:{Plus}x | DB:{Db}x | Meteor:{Met}/100k | CPs:{CpCh}% ({CpMin}-{CpMax})",
                    normalized.PresetName,
                    normalized.ExperienceRate,
                    normalized.MagicExpRate,
                    normalized.WeaponSkillExpRate,
                    normalized.SilverDropRate,
                    normalized.SilverDropChancePercent,
                    normalized.EquipmentDropRate,
                    normalized.QualityItemRate,
                    normalized.SocketDropRate,
                    normalized.PlusOneDropRate,
                    normalized.DragonBallDropRate,
                    normalized.MeteorDropPer100k,
                    normalized.MonsterCpChancePercent,
                    normalized.MonsterCpMin,
                    normalized.MonsterCpMax);

                if (processTriggers)
                {
                    if (normalized.ReloadActionsStamp > 0 && normalized.ReloadActionsStamp != lastReloadActionsStamp)
                    {
                        lastReloadActionsStamp = normalized.ReloadActionsStamp;
                        await EventManager.LoadActionsAsync();
                        LuaScriptManager.Reload();
                        logger.LogInformation("[MoonQueen Studio] Reloaded cq_action, cq_task, and Lua scripts live!");
                    }
                    else if (lastReloadActionsStamp == 0)
                    {
                        lastReloadActionsStamp = normalized.ReloadActionsStamp;
                    }

                    if (!string.IsNullOrWhiteSpace(normalized.BroadcastMessage)
                        && normalized.BroadcastStamp > 0
                        && normalized.BroadcastStamp != lastBroadcastStamp)
                    {
                        lastBroadcastStamp = normalized.BroadcastStamp;
                        TalkChannel ch = normalized.BroadcastChannel.Equals("Center", StringComparison.OrdinalIgnoreCase)
                            ? TalkChannel.Center
                            : normalized.BroadcastChannel.Equals("Talk", StringComparison.OrdinalIgnoreCase)
                                ? TalkChannel.Talk
                                : TalkChannel.TopLeft;
                        await BroadcastWorldMsgAsync(normalized.BroadcastMessage, ch, Color.Gold);
                        logger.LogInformation("[MoonQueen Studio] Broadcasted live world message: {Msg}", normalized.BroadcastMessage);
                    }
                    else if (lastBroadcastStamp == 0)
                    {
                        lastBroadcastStamp = normalized.BroadcastStamp;
                    }
                }
                else
                {
                    lastReloadActionsStamp = normalized.ReloadActionsStamp;
                    lastBroadcastStamp = normalized.BroadcastStamp;
                }

                await WriteStatusSnapshotAsync("Reloaded");
                return true;
            }
            catch (IOException)
            {
                // File may still be flushing from editor/GUI; next 1s tick will retry cleanly.
                return false;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[MoonQueen Studio] Failed to parse MoonQueen.Rates.json; keeping previous safe rates.");
                return false;
            }
        }

        public static string FormatSummary()
        {
            var r = current;
            string cpPart = r.MonsterCpChancePercent > 0
                ? $" | Monster CPs: {r.MonsterCpChancePercent:0.##}% ({r.MonsterCpMin}-{r.MonsterCpMax})"
                : string.Empty;
            return $"[{DeveloperRightsGuard.BrandName} Rates - {r.PresetName}] EXP: {r.ExperienceRate:0.##}x | Magic: {r.MagicExpRate:0.##}x | Weapon: {r.WeaponSkillExpRate:0.##}x | Silver: {r.SilverDropRate:0.##}x | Drop: {r.EquipmentDropRate:0.##}x | Quality: {r.QualityItemRate:0.##}x | Socket: {r.SocketDropRate:0.##}x | +1: {r.PlusOneDropRate:0.##}x | DB: {r.DragonBallDropRate:0.##}x{cpPart}";
        }

        private static async Task WriteStatusSnapshotAsync(string state)
        {
            try
            {
                var onlineUsers = RoleManager.QueryUserSet()
                    .OrderByDescending(u => u.BattlePower)
                    .Select(u => new
                    {
                        id = u.Identity,
                        name = u.Name,
                        level = u.Level,
                        reborn = u.Metempsychosis,
                        profession = u.Profession,
                        map_id = u.MapIdentity,
                        map_name = u.Map?.Name ?? $"Map #{u.MapIdentity}",
                        x = u.X,
                        y = u.Y,
                        silvers = u.Silvers,
                        cps = u.ConquerPoints,
                        bound_cps = u.ConquerPointsBound,
                        vip = u.BaseVipLevel,
                        bp = u.BattlePower,
                        pk = u.PkPoints,
                        syndicate = u.Syndicate?.Name ?? string.Empty,
                        is_alive = u.IsAlive,
                        is_blessed = u.IsBlessed,
                        is_jailed = u.MapIdentity == 6002 || u.MapIdentity == 6000
                    })
                    .ToList();

                var spawnedBosses = RoleManager.QueryRoles(r => r is Monster m && !m.HasGenerator && m.IsAlive)
                    .Cast<Monster>()
                    .OrderByDescending(m => m.MaxLife)
                    .Take(100)
                    .Select(m => new
                    {
                        role_id = m.Identity,
                        monster_type = m.Type,
                        name = m.Name,
                        level = m.Level,
                        life = m.Life,
                        max_life = m.MaxLife,
                        map_id = m.MapIdentity,
                        map_name = m.Map?.Name ?? $"Map #{m.MapIdentity}",
                        x = m.X,
                        y = m.Y
                    })
                    .ToList();

                List<StudioCommandResult> resultsCopy;
                lock (recentResults)
                {
                    resultsCopy = recentResults.ToList();
                }

                var snap = new
                {
                    server_name = DeveloperRightsGuard.BrandName,
                    developer = DeveloperRightsGuard.DeveloperId,
                    state,
                    online_players = RoleManager.OnlinePlayers,
                    max_online = RoleManager.MaxOnlinePlayers,
                    last_reload_at = lastReloadTime.ToString("yyyy-MM-dd HH:mm:ss"),
                    updated_at = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                    updated_unix = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                    active_rates = current,
                    promo_codes_count = currentPromos?.Codes?.Count(c => c.Enabled) ?? 0,
                    auto_broadcast_enabled = currentAutoBroadcast?.Enabled ?? false,
                    auto_broadcast_count = currentAutoBroadcast?.Items?.Count(i => i.Enabled) ?? 0,
                    custom_drop_monsters_count = currentCustomDrops?.Count ?? 0,
                    events = EventManager.GetStudioEventsSnapshot(),
                    online_players_list = onlineUsers,
                    spawned_bosses = spawnedBosses,
                    recent_results = resultsCopy
                };
                string tmp = StatusFilePath + ".tmp";
                await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(snap, jsonOptions));
                File.Move(tmp, StatusFilePath, overwrite: true);
            }
            catch
            {
                // Status file is advisory for GUI feedback; never interrupt server loop.
            }
        }

        private static TalkChannel ResolveTalkChannel(string channelName)
        {
            if (string.IsNullOrWhiteSpace(channelName))
            {
                return TalkChannel.TopLeft;
            }
            if (channelName.Equals("Center", StringComparison.OrdinalIgnoreCase))
            {
                return TalkChannel.Center;
            }
            if (channelName.Equals("Talk", StringComparison.OrdinalIgnoreCase))
            {
                return TalkChannel.Talk;
            }
            if (channelName.Equals("Broadcast", StringComparison.OrdinalIgnoreCase))
            {
                return TalkChannel.Broadcast;
            }
            return TalkChannel.TopLeft;
        }

        private static async Task ReloadPromosIfChangedAsync(bool force)
        {
            if (!File.Exists(PromosFilePath))
            {
                if (force)
                {
                    currentPromos = new StudioPromoEnvelope();
                }
                return;
            }

            long ticks = File.GetLastWriteTimeUtc(PromosFilePath).Ticks;
            if (!force && (ticks == 0 || ticks == lastPromosTicks))
            {
                return;
            }

            await promoLock.WaitAsync();
            try
            {
                string raw = await File.ReadAllTextAsync(PromosFilePath);
                var parsed = string.IsNullOrWhiteSpace(raw)
                    ? new StudioPromoEnvelope()
                    : JsonSerializer.Deserialize<StudioPromoEnvelope>(raw, jsonOptions) ?? new StudioPromoEnvelope();
                parsed.Codes ??= new List<StudioPromoCode>();
                currentPromos = parsed;
                lastPromosTicks = ticks;
            }
            catch (IOException)
            {
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[MoonQueen Studio] Could not parse MoonQueen.Studio.Promos.json.");
                lastPromosTicks = ticks;
            }
            finally
            {
                promoLock.Release();
            }
        }

        public static async Task RedeemPromoCodeInGameAsync(Character user, string rawParam)
        {
            if (user == null)
            {
                return;
            }

            string code = (rawParam ?? string.Empty).Trim().ToUpperInvariant();
            if (string.IsNullOrWhiteSpace(code))
            {
                await user.SendAsync("Usage: /redeem <CODE>  (Example: /redeem MOON2026)", TalkChannel.Talk, Color.Yellow);
                return;
            }

            await promoLock.WaitAsync();
            try
            {
                if (File.Exists(PromosFilePath))
                {
                    long diskTicks = File.GetLastWriteTimeUtc(PromosFilePath).Ticks;
                    if (diskTicks != lastPromosTicks)
                    {
                        string raw = await File.ReadAllTextAsync(PromosFilePath);
                        if (!string.IsNullOrWhiteSpace(raw))
                        {
                            currentPromos = JsonSerializer.Deserialize<StudioPromoEnvelope>(raw, jsonOptions) ?? new StudioPromoEnvelope();
                            lastPromosTicks = diskTicks;
                        }
                    }
                }

                var promo = currentPromos?.Codes?.FirstOrDefault(c =>
                    !string.IsNullOrWhiteSpace(c.Code)
                    && c.Code.Trim().Equals(code, StringComparison.OrdinalIgnoreCase));

                if (promo == null || !promo.Enabled)
                {
                    await user.SendAsync($"[{DeveloperRightsGuard.BrandName}] Invalid or inactive gift code '{code}'.", TalkChannel.Talk, Color.Red);
                    return;
                }

                long nowUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                if (promo.ExpiresUnix > 0 && nowUnix > promo.ExpiresUnix)
                {
                    await user.SendAsync($"[{DeveloperRightsGuard.BrandName}] Gift code '{code}' has expired.", TalkChannel.Talk, Color.Red);
                    return;
                }

                if (promo.MaxUses > 0 && promo.UsedCount >= promo.MaxUses)
                {
                    await user.SendAsync($"[{DeveloperRightsGuard.BrandName}] Gift code '{code}' has reached its maximum redemptions ({promo.MaxUses}).", TalkChannel.Talk, Color.Red);
                    return;
                }

                if (promo.MinLevel > 1 && user.Level < promo.MinLevel)
                {
                    await user.SendAsync($"[{DeveloperRightsGuard.BrandName}] You must be at least Level {promo.MinLevel} to redeem '{code}'.", TalkChannel.Talk, Color.Yellow);
                    return;
                }

                promo.RedeemedAccounts ??= new List<uint>();
                promo.RedeemedPlayers ??= new List<uint>();
                promo.RedeemedIps ??= new List<string>();
                promo.RedeemedMacs ??= new List<string>();
                promo.RecentClaims ??= new List<StudioPromoRedemptionLog>();

                uint accId = user.Client?.AccountIdentity ?? 0;
                string ip = user.Client?.IpAddress ?? string.Empty;
                string mac = user.Client?.MacAddress ?? string.Empty;

                if ((accId > 0 && promo.RedeemedAccounts.Contains(accId)) || promo.RedeemedPlayers.Contains(user.Identity))
                {
                    await user.SendAsync($"[{DeveloperRightsGuard.BrandName}] You have already redeemed code '{code}'.", TalkChannel.Talk, Color.Yellow);
                    return;
                }

                if (promo.OnePerIpOrMac)
                {
                    if (!string.IsNullOrWhiteSpace(ip) && promo.RedeemedIps.Contains(ip, StringComparer.OrdinalIgnoreCase))
                    {
                        await user.SendAsync($"[{DeveloperRightsGuard.BrandName}] This code has already been claimed from your network.", TalkChannel.Talk, Color.Yellow);
                        return;
                    }
                    if (!string.IsNullOrWhiteSpace(mac)
                        && !mac.Equals("Unknown", StringComparison.OrdinalIgnoreCase)
                        && !mac.Equals("000000000000", StringComparison.OrdinalIgnoreCase)
                        && promo.RedeemedMacs.Contains(mac, StringComparer.OrdinalIgnoreCase))
                    {
                        await user.SendAsync($"[{DeveloperRightsGuard.BrandName}] This code has already been claimed on this device.", TalkChannel.Talk, Color.Yellow);
                        return;
                    }
                }

                if (promo.ItemType > 0)
                {
                    DbItemtype it = ItemManager.GetItemtype(promo.ItemType);
                    if (it == null)
                    {
                        await user.SendAsync($"[{DeveloperRightsGuard.BrandName}] Gift item #{promo.ItemType} is unavailable.", TalkChannel.Talk, Color.Red);
                        return;
                    }
                    int needSlots = Math.Clamp(promo.ItemAmount <= 0 ? 1 : promo.ItemAmount, 1, 20);
                    if (!user.UserPackage.IsPackSpare(needSlots, it.Type))
                    {
                        await user.SendAsync($"[{DeveloperRightsGuard.BrandName}] Please free at least {needSlots} inventory slot(s) and try /redeem {code} again.", TalkChannel.Talk, Color.Yellow);
                        return;
                    }
                }

                var rewardCmd = new StudioCommandItem
                {
                    SilverAmount = promo.SilverAmount,
                    CpAmount = promo.CpAmount,
                    BoundCpAmount = promo.BoundCpAmount,
                    BlessingHours = promo.BlessingHours,
                    VipLevel = promo.VipLevel,
                    ItemType = promo.ItemType,
                    ItemAmount = promo.ItemAmount <= 0 ? 1 : promo.ItemAmount,
                    Plus = promo.Plus,
                    Bless = promo.Bless,
                    Enchant = promo.Enchant,
                    SocketOne = promo.SocketOne,
                    SocketTwo = promo.SocketTwo,
                    Bound = promo.Bound,
                    Message = $"[{DeveloperRightsGuard.BrandName} Promo '{code}'] Congratulations! You claimed: "
                };

                string summary = await ApplyRewardPackageToUserAsync(user, new StudioCommandItem
                {
                    SilverAmount = rewardCmd.SilverAmount,
                    CpAmount = rewardCmd.CpAmount,
                    BoundCpAmount = rewardCmd.BoundCpAmount,
                    BlessingHours = rewardCmd.BlessingHours,
                    VipLevel = rewardCmd.VipLevel,
                    ItemType = rewardCmd.ItemType,
                    ItemAmount = rewardCmd.ItemAmount,
                    Plus = rewardCmd.Plus,
                    Bless = rewardCmd.Bless,
                    Enchant = rewardCmd.Enchant,
                    SocketOne = rewardCmd.SocketOne,
                    SocketTwo = rewardCmd.SocketTwo,
                    Bound = rewardCmd.Bound,
                    Message = string.IsNullOrWhiteSpace(promo.Title)
                        ? $"[{DeveloperRightsGuard.BrandName} Gift Code '{code}'] Claimed successfully!"
                        : $"[{DeveloperRightsGuard.BrandName} - {promo.Title}] Code '{code}' claimed successfully!"
                });

                promo.UsedCount++;
                if (accId > 0) promo.RedeemedAccounts.Add(accId);
                promo.RedeemedPlayers.Add(user.Identity);
                if (!string.IsNullOrWhiteSpace(ip) && !promo.RedeemedIps.Contains(ip, StringComparer.OrdinalIgnoreCase))
                {
                    promo.RedeemedIps.Add(ip);
                }
                if (!string.IsNullOrWhiteSpace(mac) && !promo.RedeemedMacs.Contains(mac, StringComparer.OrdinalIgnoreCase))
                {
                    promo.RedeemedMacs.Add(mac);
                }

                promo.RecentClaims.Insert(0, new StudioPromoRedemptionLog
                {
                    AccountId = accId,
                    PlayerId = user.Identity,
                    PlayerName = user.Name,
                    Ip = ip,
                    ClaimedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")
                });
                if (promo.RecentClaims.Count > 50)
                {
                    promo.RecentClaims.RemoveRange(50, promo.RecentClaims.Count - 50);
                }

                string tmp = PromosFilePath + ".tmp";
                await File.WriteAllTextAsync(tmp, JsonSerializer.Serialize(currentPromos, jsonOptions));
                File.Move(tmp, PromosFilePath, overwrite: true);
                lastPromosTicks = File.GetLastWriteTimeUtc(PromosFilePath).Ticks;

                logger.LogInformation("[MoonQueen Promo] [{Player}] redeemed '{Code}' -> {Summary}", user.Name, code, summary);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[MoonQueen Promo] Error redeeming code '{Code}' for {User}", code, user.Name);
            }
            finally
            {
                promoLock.Release();
            }
        }

        private static async Task ReloadAutoBroadcastIfChangedAsync(bool force)
        {
            if (!File.Exists(AutoBroadcastFilePath))
            {
                if (force)
                {
                    currentAutoBroadcast = new StudioAutoBroadcastEnvelope();
                }
                return;
            }

            long ticks = File.GetLastWriteTimeUtc(AutoBroadcastFilePath).Ticks;
            if (!force && (ticks == 0 || ticks == lastAutoBroadcastTicks))
            {
                return;
            }

            try
            {
                string raw = await File.ReadAllTextAsync(AutoBroadcastFilePath);
                var parsed = string.IsNullOrWhiteSpace(raw)
                    ? new StudioAutoBroadcastEnvelope()
                    : JsonSerializer.Deserialize<StudioAutoBroadcastEnvelope>(raw, jsonOptions) ?? new StudioAutoBroadcastEnvelope();
                parsed.Items ??= new List<StudioAutoBroadcastItem>();
                currentAutoBroadcast = parsed;
                lastAutoBroadcastTicks = ticks;

                DateTime nowUtc = DateTime.UtcNow;
                int idx = 0;
                foreach (var item in parsed.Items)
                {
                    if (!string.IsNullOrWhiteSpace(item.Id))
                    {
                        autoBroadcastLastIntervalUtc.TryAdd(item.Id, nowUtc.AddSeconds(idx * 5));
                        idx++;
                    }
                }
            }
            catch (IOException)
            {
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[MoonQueen Studio] Could not parse MoonQueen.Studio.AutoBroadcast.json.");
                lastAutoBroadcastTicks = ticks;
            }
        }

        private static async Task ProcessAutoBroadcastTickAsync()
        {
            var cfg = currentAutoBroadcast;
            if (cfg == null || !cfg.Enabled || cfg.Items == null || cfg.Items.Count == 0)
            {
                return;
            }

            DateTime nowUtc = DateTime.UtcNow;
            DateTime nowLocal = DateTime.Now;
            string hhmm = nowLocal.ToString("HH:mm");
            string today = nowLocal.ToString("yyyy-MM-dd");

            foreach (var item in cfg.Items)
            {
                if (item == null || !item.Enabled || string.IsNullOrWhiteSpace(item.Message))
                {
                    continue;
                }

                string key = string.IsNullOrWhiteSpace(item.Id) ? item.Message : item.Id;
                if (string.Equals(item.Mode, "daily", StringComparison.OrdinalIgnoreCase))
                {
                    if (!string.IsNullOrWhiteSpace(item.DailyTime)
                        && string.Equals(item.DailyTime.Trim(), hhmm, StringComparison.Ordinal))
                    {
                        if (!autoBroadcastLastDailyDate.TryGetValue(key, out string lastDate) || lastDate != today)
                        {
                            autoBroadcastLastDailyDate[key] = today;
                            await BroadcastWorldMsgAsync(item.Message.Trim(), ResolveTalkChannel(item.Channel), Color.Gold);
                        }
                    }
                }
                else
                {
                    int mins = Math.Clamp(item.IntervalMinutes <= 0 ? 15 : item.IntervalMinutes, 1, 1440);
                    DateTime lastUtc = autoBroadcastLastIntervalUtc.GetOrAdd(key, nowUtc);
                    if ((nowUtc - lastUtc).TotalMinutes >= mins)
                    {
                        autoBroadcastLastIntervalUtc[key] = nowUtc;
                        await BroadcastWorldMsgAsync(item.Message.Trim(), ResolveTalkChannel(item.Channel), Color.Gold);
                    }
                }
            }
        }

        private static async Task ReloadCustomDropsIfChangedAsync(bool force)
        {
            if (!File.Exists(CustomDropsFilePath))
            {
                if (force)
                {
                    currentCustomDrops = new Dictionary<uint, StudioMonsterDropProfile>();
                    hasCustomMonsterDrops = false;
                }
                return;
            }

            long ticks = File.GetLastWriteTimeUtc(CustomDropsFilePath).Ticks;
            if (!force && (ticks == 0 || ticks == lastCustomDropsTicks))
            {
                return;
            }

            try
            {
                string raw = await File.ReadAllTextAsync(CustomDropsFilePath);
                var parsed = string.IsNullOrWhiteSpace(raw)
                    ? new StudioCustomDropsEnvelope()
                    : JsonSerializer.Deserialize<StudioCustomDropsEnvelope>(raw, jsonOptions) ?? new StudioCustomDropsEnvelope();

                var dict = new Dictionary<uint, StudioMonsterDropProfile>();
                if (parsed.Profiles != null)
                {
                    foreach (var prof in parsed.Profiles)
                    {
                        if (prof != null && prof.Enabled && prof.MonsterType > 0 && prof.Rules != null && prof.Rules.Count > 0)
                        {
                            dict[prof.MonsterType] = prof;
                        }
                    }
                }

                currentCustomDrops = dict;
                hasCustomMonsterDrops = dict.Count > 0;
                lastCustomDropsTicks = ticks;
                logger.LogInformation("[MoonQueen Studio] Loaded custom drop profiles for {Count} monster type(s).", dict.Count);
            }
            catch (IOException)
            {
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[MoonQueen Studio] Could not parse MoonQueen.Studio.CustomDrops.json.");
                lastCustomDropsTicks = ticks;
            }
        }

        public static async Task<bool> ProcessCustomMonsterDropsAsync(Monster monster, Character killer)
        {
            if (monster == null || !hasCustomMonsterDrops)
            {
                return false;
            }

            var map = currentCustomDrops;
            if (map == null || !map.TryGetValue(monster.Type, out var profile) || profile == null || !profile.Enabled || profile.Rules == null)
            {
                return false;
            }

            var rng = Random.Shared;
            foreach (var rule in profile.Rules)
            {
                if (rule == null || rule.ChancePercent <= 0d)
                {
                    continue;
                }

                if (rule.ChancePercent < 100d && (rng.NextDouble() * 100d) >= rule.ChancePercent)
                {
                    continue;
                }

                if (killer != null)
                {
                    if (rule.BonusSilver > 0)
                    {
                        await killer.AwardMoneyAsync(rule.BonusSilver);
                    }
                    if (rule.BonusCps > 0)
                    {
                        await killer.ChangeConquerPointsAsync(rule.BonusCps, true);
                    }
                    if (rule.BonusBoundCps > 0)
                    {
                        await killer.ChangeBoundConquerPointsAsync(rule.BonusBoundCps, true);
                    }
                }

                if (rule.ItemType > 0)
                {
                    DbItemtype itemtype = ItemManager.GetItemtype(rule.ItemType);
                    if (itemtype != null)
                    {
                        int minQ = Math.Clamp(rule.MinCount <= 0 ? 1 : rule.MinCount, 1, 15);
                        int maxQ = Math.Clamp(rule.MaxCount < minQ ? minQ : rule.MaxCount, minQ, 15);
                        int qty = maxQ > minQ ? rng.Next(minQ, maxQ + 1) : minQ;
                        int dropped = 0;

                        for (int i = 0; i < qty; i++)
                        {
                            if (await monster.DropCustomStudioItemAsync(itemtype, killer, rule.Plus, rule.Bless, rule.Sockets, rule.Bound))
                            {
                                dropped++;
                            }
                        }

                        if (dropped > 0 && rule.WorldAnnounce)
                        {
                            string plusTag = rule.Plus > 0 ? $" (+{rule.Plus})" : string.Empty;
                            string killerLabel = killer?.Name ?? "A brave warrior";
                            await BroadcastWorldMsgAsync(
                                $"[{DeveloperRightsGuard.BrandName} Boss Drop] {killerLabel} defeated {monster.Name} and dropped {dropped}x {itemtype.Name}{plusTag}!",
                                TalkChannel.Center,
                                Color.Gold);
                        }
                    }
                }
            }

            return string.Equals(profile.Mode, "override", StringComparison.OrdinalIgnoreCase);
        }
    }
}
