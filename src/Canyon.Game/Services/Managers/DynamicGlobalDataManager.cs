using Canyon.Database.Entities;
using Canyon.Game.Database;
using Canyon.Game.Database.Repositories;
using Canyon.Game.Sockets.Game.Packets;
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
        private static long lastBroadcastStamp = 0;
        private static long lastReloadActionsStamp = 0;
        private static DateTime lastReloadTime = DateTime.Now;
        private static int statusTickCounter = 0;

        public static string RatesFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MoonQueen.Rates.json");
        public static string StatusFilePath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MoonQueen.Rates.status.json");

        public static ServerConfiguration.ServerRatesConfiguration Current => current;

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

            await WriteStatusSnapshotAsync("Initialized");
        }

        public static async Task OnTimerAsync()
        {
            try
            {
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
                if (statusTickCounter >= 5)
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
                    active_rates = current
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
    }
}
