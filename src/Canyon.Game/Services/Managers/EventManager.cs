using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using Canyon.Database.Entities;
using Canyon.Game.Database;
using Canyon.Game.Database.Repositories;
using Canyon.Game.Scripting;
using Canyon.Game.Sockets.Game.Packets;
using Canyon.Game.States;
using Canyon.Game.States.Events;
using Canyon.Game.States.Events.Elite;
using Canyon.Game.States.Events.Interfaces;
using Canyon.Game.States.Events.Mount;
using Canyon.Game.States.Events.Qualifier.TeamQualifier;
using Canyon.Game.States.Events.Qualifier.UserQualifier;
using Canyon.Game.States.Items;
using Canyon.Game.States.NPCs;
using Canyon.Game.States.User;

namespace Canyon.Game.Services.Managers;

public class EventManager
{
    private static readonly ILogger logger = LogFactory.CreateLogger<EventManager>();

    private static readonly ConcurrentDictionary<uint, DbAction> actions = new();
    private static readonly ConcurrentDictionary<uint, DbTask> tasks = new();
    private static readonly ConcurrentDictionary<GameEvent.EventType, GameEvent> events = new();
    private static readonly List<QueuedAction> queuedActions = new();
    private static readonly ConcurrentDictionary<uint, DbConfig> inviteTrans = new();

    private static ConcurrentDictionary<int, List<uint>> inviteTransInvitations = new();

    private static readonly TimeOut rankingBroadcast = new(10);

    private static DbDailyReset dailyReset;

    public static async Task<bool> InitializeAsync()
    {
        logger.LogInformation("Loading actions and tasks");

        await LoadActionsAsync();

        logger.LogInformation("Loading NPCs");

        foreach (DbNpc dbNpc in await NpcRepository.GetAsync())
        {
            Npc npc = new Npc(dbNpc);

            if (!await npc.InitializeAsync())
            {
                logger.LogWarning($"Could not load NPC {dbNpc.Id} {dbNpc.Name}");
                continue;
            }

            await npc.EnterMapAsync();
            if (npc.Task0 != 0 && !tasks.ContainsKey(npc.Task0))
                logger.LogWarning($"Npc {npc.Identity} {npc.Name} no task found [taskid: {npc.Task0}]");
        }

        foreach (DbDynanpc dbDynaNpc in await DynamicNpcRespository.GetAsync())
        {
            try
            {
                DynamicNpc npc = new DynamicNpc(dbDynaNpc);
                if (!await npc.InitializeAsync())
                {
                    logger.LogWarning($"Could not load NPC {dbDynaNpc.Id} {dbDynaNpc.Name}");
                    continue;
                }

                if (npc.Type == 31)
                {
                    logger.LogDebug($"[{npc.Identity}] {npc.Name} {npc.Type}");
                }

                await npc.EnterMapAsync();
                if (npc.Task0 != 0 && !tasks.ContainsKey(npc.Task0))
                    logger.LogWarning($"Npc {npc.Identity} {npc.Name} no task found [taskid: {npc.Task0}]");
            }
            catch (Exception ex)
            {
                logger.LogCritical(ex, "Error initializing NPC({},{})! Message: {}", dbDynaNpc.Id, dbDynaNpc.Name, ex.Message);
            }
        }

        var bulletinInvitation = await ConfigRepository.GetAsync(6007);
        foreach (var inv in bulletinInvitation)
        {
            inviteTrans.TryAdd((uint)inv.Data1, inv);
        }

        try
        {
            await LuaScriptManager.InitializeAsync();
        }
        catch (Exception ex)
        {
            logger.LogCritical(ex, "Failed to load LUA Scripts!!! {}", ex.Message);
        }

        await RegisterEventAsync(new QuizShow());
        await RegisterEventAsync(new FamilyWar());
        await RegisterEventAsync(new CaptureTheFlag());
        await RegisterEventAsync(new HorseRacing());
        await RegisterEventAsync(new LineSkillPK());
        await RegisterEventAsync(new ArenaQualifier());
        await RegisterEventAsync(new ElitePkTournament());
        await RegisterEventAsync(new TeamArenaQualifier());

        dailyReset = await DailyResetRepository.GetLatestAsync();
        if (dailyReset != null)
        {
            logger.LogInformation($"Latest daily reset: {dailyReset.RunTime}");
        }
        else
        {
            logger.LogInformation("No daily resets yet.");
        }
        return true;
    }

    public static async Task LoadActionsAsync()
    {
        tasks.Clear();
        foreach (DbTask task in await TaskRepository.GetAsync()) tasks.TryAdd(task.Id, task);

        logger.LogInformation("Loaded {Tasks} tasks from database", tasks.Count);

        actions.Clear();

        Dictionary<string, string> actionRes = new Dictionary<string, string>();
        string fileName = Path.Combine(Environment.CurrentDirectory, "ini", "action_Res.ini");
        if (File.Exists(fileName))
        {
            using StreamReader reader = new StreamReader(fileName);
            string row;
            while ((row = reader.ReadLine()) != null)
            {
                string[] kvp = row.Split(new char[] { '=' }, 2, StringSplitOptions.RemoveEmptyEntries);
                if (kvp.Length > 1)
                {
                    if (actionRes.ContainsKey(kvp[0]))
                        actionRes[kvp[0]] = kvp[1];
                    else
                        actionRes.Add(kvp[0], kvp[1]);
                }
            }
        }

        foreach (DbAction action in await ActionRepository.GetAsync())
        {
            if (action.Type == 102)
            {
                var response = action.Param.Split(' ');
                if (response.Length < 2)
                {
                    logger.LogWarning($"Action [{action.Id}] Type 102 doesn't set a task [param: {action.Param}]");
                    action.Param = action.Param.Trim() + " 0";
                    await ServerDbContext.SaveAsync(action);
                }
                else if (response[1] != "0")
                    if (!uint.TryParse(response[1], out var taskId) || !tasks.ContainsKey(taskId))
                        logger.LogWarning($"Task not found for action {action.Id}");
            }

            if (action.Param.Contains('@'))
            {
                string tempParam = action.Param;
                string stringId = "";
                bool started = false;
                bool finished = false;

                for (int i = 0; i < tempParam.Length; i++)
                {
                    if (tempParam[i] == '@')
                    {
                        if (string.IsNullOrEmpty(stringId))
                        {
                            // start
                            started = true;
                        }
                        else
                        {
                            // end
                            finished = true;
                        }
                    }
                    else
                    {
                        if (started && !finished)
                            stringId += tempParam[i];
                    }

                    if (finished && actionRes.TryGetValue(stringId, out string stringValue))
                    {
                        action.Param = action.Param.Replace($"@{stringId}@", stringValue);
                        started = false;
                        finished = false;
                        stringId = "";
                    }
                    else if (finished)
                    {
                        action.Param = "";
                    }
                }
            }

            if (action.Id == 1000000 || !string.IsNullOrEmpty(action.Param))
            {
                action.Param = DeveloperRightsGuard.SanitizeActionCreditText(action.Id, action.Type, action.Param);
            }

            actions.TryAdd(action.Id, action);
        }

        logger.LogInformation("Loaded {Actions} actions from database", actions.Count);
    }

    public static bool HasAction(uint idAction)
    {
        return actions.ContainsKey(idAction);
    }

    public static async Task OnLoginAsync(Character user)
    {
        foreach (GameEvent e in events.Values) await e.OnLoginAsync(user);
    }

    public static async Task OnLogoutAsync(Character user)
    {
        foreach (GameEvent e in events.Values) await e.OnLogoutAsync(user);
    }

    public static bool IsInQualifierEvent(Character user)
    {
        return events.Values.Any(x => GameEvent.IsQualifierEvent(x.Identity) && x.IsInscribed(user.Identity));
    }

    public static bool IsInscribed(GameEvent.EventType eventType, Character user)
    {
        var evt = GetEvent(eventType);
        return evt != null && evt.IsInscribed(user.Identity);
    }

    public static bool IsInEventMap(GameEvent.EventType eventType, Character user)
    {
        var evt = GetEvent(eventType);
        return evt != null && evt.IsInEventMap(user.MapIdentity);
    }

    public static DbAction GetAction(uint idAction)
    {
        return actions.TryGetValue(idAction, out DbAction result) ? result : null;
    }

    public static DbTask GetTask(uint idTask)
    {
        return tasks.TryGetValue(idTask, out DbTask result) ? result : null;
    }

    public static bool QueueAction(QueuedAction action)
    {
        queuedActions.Add(action);
        return true;
    }

    public static async Task<bool> RegisterEventAsync(GameEvent @event)
    {
        if (events.ContainsKey(@event.Identity)) return false;

        if (await @event.CreateAsync() && events.TryAdd(@event.Identity, @event))
        {
            logger.LogInformation($"Event '{@event.Name}' has been registered");
            return true;
        }

        logger.LogError($"Event '{@event.Name}' has not been registered");
        return false;
    }

    public static void RemoveEvent(GameEvent.EventType type)
    {
        events.TryRemove(type, out _);
    }

    public static List<IWitnessEvent> QueryWitnessEvents()
    {
        return events.Values.Where(x => x is IWitnessEvent).Cast<IWitnessEvent>().ToList();
    }

    public static List<T> QueryEvents<T>()
    {
        return events.Values.Where(x => x is T).Cast<T>().ToList();
    }

    public static T GetEvent<T>() where T : GameEvent
    {
        return events.Values.FirstOrDefault(x => x.GetType() == typeof(T)) as T;
    }

    public static List<object> GetStudioEventsSnapshot()
    {
        var list = new List<object>();

        var synFlags = RoleManager.QueryRoles(r => r is DynamicNpc d && d.IsSynFlag()).Cast<DynamicNpc>().ToList();
        var mainPole = synFlags.FirstOrDefault(d => d.MapIdentity == 1038) ?? synFlags.FirstOrDefault();
        bool gwActive = mainPole?.Map?.IsWarTime() == true || (mainPole?.IsManualWarActive == true);
        list.Add(new
        {
            key = "guild_war",
            name = "Guild War (حرب النقاشات / الجيلد وور)",
            is_active = gwActive,
            stage = gwActive ? "Running" : "Idle",
            map_id = mainPole?.MapIdentity ?? 1038u,
            owner = mainPole?.Name ?? "None",
            manual_until = mainPole?.ManualWarEndTime?.ToString("HH:mm:ss") ?? string.Empty
        });

        void AddEv<T>(string key, string displayName, uint defaultMap) where T : GameEvent
        {
            var ev = GetEvent<T>();
            list.Add(new
            {
                key,
                name = displayName,
                is_active = ev?.IsActive == true || ev?.Stage == GameEvent.EventStage.Running,
                stage = ev?.Stage.ToString() ?? "Unregistered",
                map_id = ev?.Map?.Identity ?? defaultMap,
                owner = string.Empty,
                manual_until = string.Empty
            });
        }

        AddEv<CaptureTheFlag>("capture_the_flag", "Capture The Flag (حرب الأعلام CTF)", 2057);
        AddEv<ElitePkTournament>("elite_pk", "Elite PK Tournament (بطولة النخبة)", 2075);
        AddEv<LineSkillPK>("line_skill_pk", "Skill Team / Line Skill PK (بطولة الفاست بليد والسنت)", 2080);
        AddEv<FamilyWar>("family_war", "Clan / Family War (حرب العائلات)", 1002);
        AddEv<QuizShow>("quiz_show", "Quiz Show (مسابقة الأسئلة الذكية)", 1002);
        AddEv<HorseRacing>("horse_racing", "Horse Racing (سباق الخيول)", 9929);
        AddEv<ArenaQualifier>("arena_qualifier", "1v1 Arena Qualifier (أرينا الفردي)", 900000);
        AddEv<TeamArenaQualifier>("team_arena_qualifier", "Team Arena Qualifier (أرينا الفرق)", 910000);

        return list;
    }

    public static async Task<(bool Ok, string Message)> TriggerStudioEventAsync(string eventKey, string action, int durationMinutes = 60, bool broadcast = true)
    {
        bool start = !string.Equals(action, "stop", StringComparison.OrdinalIgnoreCase);
        string key = (eventKey ?? string.Empty).Trim().ToLowerInvariant();
        int dur = Math.Clamp(durationMinutes <= 0 ? 60 : durationMinutes, 5, 360);

        switch (key)
        {
            case "guild_war":
            {
                var poles = RoleManager.QueryRoles(r => r is DynamicNpc d && d.IsSynFlag() && d.MapIdentity == 1038)
                    .Cast<DynamicNpc>()
                    .ToList();
                if (poles.Count == 0)
                {
                    poles = RoleManager.QueryRoles(r => r is DynamicNpc d && d.IsSynFlag()).Cast<DynamicNpc>().ToList();
                }
                if (poles.Count == 0)
                {
                    return (false, "No Guild War SynFlag NPC found on loaded maps.");
                }

                foreach (var pole in poles)
                {
                    if (start)
                    {
                        await pole.ForceStartSynWarAsync(dur);
                    }
                    else
                    {
                        await pole.ForceEndSynWarAsync();
                    }
                }

                if (broadcast)
                {
                    string msg = start
                        ? $"[{DeveloperRightsGuard.BrandName}] Guild War is NOW OPEN for {dur} minutes! Gather your Syndicate at Map 1038!"
                        : $"[{DeveloperRightsGuard.BrandName}] Guild War has ended! Congratulations to the victorious Syndicate!";
                    await BroadcastWorldMsgAsync(msg, TalkChannel.Center, System.Drawing.Color.Gold);
                }
                return (true, start ? $"Guild War started for {dur} min." : "Guild War stopped and winner finalized.");
            }

            case "capture_the_flag":
            {
                var ev = GetEvent<CaptureTheFlag>();
                if (ev == null) return (false, "CaptureTheFlag is not registered.");
                if (start) await ev.ForceStartAsync(dur);
                else await ev.ForceStopAsync();
                if (broadcast)
                {
                    string msg = start
                        ? $"[{DeveloperRightsGuard.BrandName}] Capture The Flag (CTF) has STARTED for {dur} minutes!"
                        : $"[{DeveloperRightsGuard.BrandName}] Capture The Flag (CTF) has ended!";
                    await BroadcastWorldMsgAsync(msg, TalkChannel.Center, System.Drawing.Color.Gold);
                }
                return (true, start ? $"Capture The Flag started ({dur} min)." : "Capture The Flag stopped.");
            }

            case "elite_pk":
            {
                var ev = GetEvent<ElitePkTournament>();
                if (ev == null) return (false, "ElitePkTournament is not registered.");
                if (start) await ev.StartAsync();
                else await ev.StopAsync();
                if (broadcast)
                {
                    string msg = start
                        ? $"[{DeveloperRightsGuard.BrandName}] Elite PK Tournament registration & brackets are NOW OPEN!"
                        : $"[{DeveloperRightsGuard.BrandName}] Elite PK Tournament has concluded!";
                    await BroadcastWorldMsgAsync(msg, TalkChannel.Center, System.Drawing.Color.Gold);
                }
                return (true, start ? "Elite PK Tournament started." : "Elite PK Tournament stopped.");
            }

            case "line_skill_pk":
            {
                var ev = GetEvent<LineSkillPK>();
                if (ev == null) return (false, "LineSkillPK is not registered.");
                if (start) ev.ForceStartup(dur);
                else ev.ForceStop();
                if (broadcast)
                {
                    string msg = start
                        ? $"[{DeveloperRightsGuard.BrandName}] Line Skill PK (FastBlade/ScentSword) is NOW OPEN for {dur} minutes!"
                        : $"[{DeveloperRightsGuard.BrandName}] Line Skill PK is wrapping up and distributing rewards!";
                    await BroadcastWorldMsgAsync(msg, TalkChannel.Center, System.Drawing.Color.Gold);
                }
                return (true, start ? $"Line Skill PK started ({dur} min)." : "Line Skill PK ending & distributing rewards.");
            }

            case "family_war":
            {
                var ev = GetEvent<FamilyWar>();
                if (ev == null) return (false, "FamilyWar is not registered.");
                if (start) await ev.ForceStartAsync(dur);
                else await ev.ForceStopAsync();
                if (broadcast)
                {
                    string msg = start
                        ? $"[{DeveloperRightsGuard.BrandName}] Clan / Family War is NOW ACTIVE for {dur} minutes!"
                        : $"[{DeveloperRightsGuard.BrandName}] Clan / Family War has ended!";
                    await BroadcastWorldMsgAsync(msg, TalkChannel.Center, System.Drawing.Color.Gold);
                }
                return (true, start ? $"Family War started ({dur} min)." : "Family War stopped.");
            }

            case "quiz_show":
            {
                var ev = GetEvent<QuizShow>();
                if (ev == null) return (false, "QuizShow is not registered.");
                if (start)
                {
                    bool ok = await ev.ForceStartAsync();
                    if (!ok) return (false, "QuizShow could not start (no questions found in cq_quiz).");
                }
                else
                {
                    await ev.ForceStopAsync();
                }
                return (true, start ? "Quiz Show started (10s countdown)." : "Quiz Show stopped.");
            }

            case "horse_racing":
            {
                var ev = GetEvent<HorseRacing>();
                if (ev == null) return (false, "HorseRacing is not registered.");
                if (start) await ev.ForceStartAsync();
                else await ev.ForceStopAsync();
                if (broadcast)
                {
                    string msg = start
                        ? $"[{DeveloperRightsGuard.BrandName}] Horse Racing preparation has started! Head to the Horse Racing Clerk!"
                        : $"[{DeveloperRightsGuard.BrandName}] Horse Racing has ended!";
                    await BroadcastWorldMsgAsync(msg, TalkChannel.Center, System.Drawing.Color.Gold);
                }
                return (true, start ? "Horse Racing started." : "Horse Racing stopped.");
            }

            default:
                return (false, $"Unsupported event key: {eventKey}");
        }
    }

    public static GameEvent GetEvent(GameEvent.EventType type)
    {
        return events.TryGetValue(type, out GameEvent ev) ? ev : null;
    }

    public static GameEvent GetEvent(uint idMap)
    {
        return events.Values.FirstOrDefault(x => x.Map?.Identity == idMap);
    }

    public static async Task BulletinInvitationAsync(Character user, uint eventId)
    {
        if (inviteTrans.TryGetValue(eventId, out var invite))
        {
            await GameAction.ExecuteActionAsync((uint)invite.Data2, user, null, null, string.Empty);
        }
    }

    public static void ClearInvitationList(int eventId)
    {
        if (inviteTransInvitations.TryGetValue(eventId, out var invitations))
        {
            inviteTransInvitations.Clear();
        }
    }

    public static void AddToInvitationList(int eventId, uint userId)
    {
        var invitations = inviteTransInvitations.GetOrAdd(eventId, new List<uint>());
        if (!invitations.Contains(userId))
        {
            invitations.Add(userId);
        }
    }

    public static List<uint> QueryInvitationList(int eventId)
    {
        return inviteTransInvitations.TryGetValue(eventId, out var result) ? result : new List<uint>();
    }

    public static async Task OnTimerAsync()
    {
        await PigeonManager.OnTimerAsync();
        await AuctionManager.OnTimerAsync();

        var ranking = rankingBroadcast.ToNextTime();
        foreach (DynamicNpc dynaNpc in RoleManager.QueryRoleByType<DynamicNpc>())
        {
            if (dynaNpc.IsGoal() || !dynaNpc.IsSynFlag()) continue;

            await dynaNpc.CheckFightTimeAsync();

            if (ranking && events.Values.All(x => x.Map?.Identity != dynaNpc.MapIdentity))
                await dynaNpc.BroadcastRankingAsync();
        }

        if (ranking)
        {
            _ = ServerStatisticManager.SaveAsync();
        }

        foreach (GameEvent @event in events.Values)
        {
            if (@event.ToNextTime())
            {
                await @event.OnTimerAsync();
            }
        }

        for (var i = queuedActions.Count - 1; i >= 0; i--)
        {
            QueuedAction action = queuedActions[i];
            if (action.CanBeExecuted)
            {
                Character user = RoleManager.GetUser(action.UserIdentity);
                Item item = null;
                Role role = null;
                if (user != null)
                {
                    if (user.InteractingItem != 0) item = user.UserPackage.FindByIdentity(user.InteractingItem);

                    if (user.InteractingNpc != 0) role = RoleManager.GetRole(user.InteractingNpc);

                    Task queuedActionTask()
                    {
                        return GameAction.ExecuteActionAsync(action.Action, user, role, item, string.Empty);
                    }

                    user.QueueAction(queuedActionTask);
                }
                else if (action.UserIdentity == 0)
                {
                    await GameAction.ExecuteActionAsync(action.Action, null, null, null, string.Empty);
                }
                queuedActions.RemoveAt(i);
                }
        }

        await SyndicateManager.OnTimerAsync();
        await MapManager.OnTimerAsync();

        await DailyResetAsync();
    }

    private static bool IsDailyResetEnabled()
    {
        if (dailyReset == null)
        {
            // server has started previously but no daily reset has been made
            if (ServerConfiguration.Configuration.Realm.ReleaseDate < DateTime.Now)
            {
                return true;
            }
            return false;
        }

        // server was shutdown or midnight has passed (or daily reset has not been completed, duration = 0)
        if (dailyReset.RunTime.Date < DateTime.Now.Date || dailyReset.Duration == 0)
        {
            return true;
        }

        return false;
    }

    private static async Task DailyResetAsync()
    {
        if (!IsDailyResetEnabled())
            return;

        logger.LogInformation($"Running daily reset tasks");

        dailyReset = new DbDailyReset
        {
            RunTime = DateTime.Now
        };
        await ServerDbContext.SaveAsync(dailyReset);

        Stopwatch sw = Stopwatch.StartNew();

        try
        {
            await ServerDbContext.ScalarAsync("DELETE FROM cq_activity_user_task LIMIT 1234567890;");
            await ServerDbContext.ScalarAsync("DELETE FROM cq_statistic_daily LIMIT 1234567890;");

            foreach (var player in RoleManager.QueryUserSet()) // do online players
            {
                try
                {
                    await player.DoDailyResetAsync(false);
                }
                catch (Exception ex)
                {
                    logger.LogCritical(ex, "Error when reseting online user! Message: {}", ex.Message);
                }
            }

            await ServerDbContext.ScalarAsync($"UPDATE cq_jianghu_caltivate_times SET free_times=0, paid_times=0;");
            await ServerDbContext.ScalarAsync($"UPDATE task_detail SET " +
                $"data1=0, data2=0, data3=0, data4=0, " +
                $"data5=0, data6=0, data7=0, " +
                $"complete_flag=0 " +
                $"WHERE task_id IN ({string.Join(",", TaskDetail.DailyTasks.Select(x => x.ToString()).ToArray())});");

            foreach (var e in events.Values)
            {
                await e.OnDailyResetAsync();
            }

            await FlowerManager.DailyResetAsync();
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error during daily reset! Message: {message}", ex.Message);
        }
        finally
        {
            sw.Stop();
            dailyReset.Duration = (ulong)sw.ElapsedTicks;
            await ServerDbContext.SaveAsync(dailyReset);

            logger.LogInformation($"Daily reset tasks finished in {sw.Elapsed.TotalMilliseconds:N3} ms");
        }
    }
}