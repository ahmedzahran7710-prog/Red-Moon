using Canyon.Ai.Database.Repositories;
using Canyon.Ai.Managers;
using Canyon.Ai.Sockets;
using Canyon.Ai.Sockets.Packets;
using Canyon.Database.Entities;
using Canyon.Network.Packets.Ai;
using Canyon.Shared.Managers;
using System.Collections.Concurrent;
using System.Drawing;

namespace Canyon.Ai.States.World
{
    public sealed class Generator
    {
        private static readonly ILogger logger = LogFactory.CreateLogger<Generator>();
        private static uint idGenerator = 2000000;

        private readonly DbGenerator generator;
        private readonly DbMonstertype monstertype;
        private readonly Point center;
        private readonly GameMap gameMap;
        private readonly List<MonsterRatio> monsterRatios = new List<MonsterRatio>();

        private readonly ConcurrentDictionary<uint, TimeOut> awaitingReborn = new();
        private readonly ConcurrentDictionary<uint, Monster> monsters = new();

        private readonly TimeOut timeBetweenGens = new(1);

        public Generator(DbGenerator generator)
        {
            this.generator = generator;
            center = new Point(generator.BoundX + generator.BoundCx / 2, generator.BoundY + generator.BoundCy / 2);

            gameMap = MapManager.GetMap(generator.Mapid);
            if (gameMap == null)
            {
                logger.LogError($"Could not load map [{generator.Mapid}] for generator [{generator.Id}]");
                return;
            }


            if (generator.Npctype != 0 && !HasCluster)
            {
                monstertype = RoleManager.GetMonstertype(generator.Npctype);
                if (monstertype == null)
                {
                    logger.LogError($"Could not load monstertype [{generator.Npctype}] for generator [{generator.Id}]");
                    return;
                }
            }
            else if (HasCluster)
            {
                DbMonsterCluster cluster = MonsterClusterRepository.GetById(generator.Npctype);
                if (cluster != null)
                {
                    FillMonsterRatios(cluster);
                }
            }
        }

        public Generator(uint idMap, uint idMonster, ushort usX, ushort usY, ushort usCx, ushort usCy)
        {
            generator = new DbGenerator
            {
                Mapid = idMap,
                BoundX = usX,
                BoundY = usY,
                BoundCx = usCx,
                BoundCy = usCy,
                Npctype = idMonster,
                MaxNpc = 0,
                MaxPerGen = 0,
                Id = idGenerator++
            };

            center = new Point(generator.BoundX + generator.BoundCx / 2, generator.BoundY + generator.BoundCy / 2);

            gameMap = MapManager.GetMap(generator.Mapid);
            if (gameMap == null)
            {
                logger.LogError($"Could not load map [{generator.Mapid}] for generator [{generator.Id}]");
                return;
            }

            if (idMonster != 0)
            {
                monstertype = RoleManager.GetMonstertype(generator.Npctype);
                if (monstertype == null)
                {
                    logger.LogError($"Could not load monstertype [{generator.Npctype}] for generator [{generator.Id}]");
                    return;
                }
            }
        }

        public int Generated => monsters.Count;

        public bool Ready => (generator.Npctype == 0 || monsterRatios.Count > 0 || monstertype != null) && gameMap != null;

        public bool IsActive => gameMap.PlayerCount > 0 || monsters.Values.Any(x => !x.IsAlive || x.Map?.IsInstanceMap == true);

        public uint Identity => generator.Id;

        public uint RoleType => generator.Npctype;

        public int RestSeconds => Math.Max(generator.RestSecs, MIN_TIME_BETWEEN_GEN);

        public uint MapIdentity => generator?.Mapid ?? 0;

        public string MonsterName => monstertype?.Name ?? "None";

        public bool HasCluster => generator.ClusterType;

        private void FillMonsterRatios(DbMonsterCluster cluster)
        {
            int currentRatio = 0;
            if (cluster.Ratio0 != 0)
            {
                DbMonstertype monstertype = RoleManager.GetMonstertype(cluster.Monster0);
                if (monstertype == null)
                {
                    logger.LogWarning("Cluster {} has invalid monster type {}", cluster.Id, cluster.Monster0);
                }
                else
                {
                    currentRatio += cluster.Ratio0;
                    monsterRatios.Add(new MonsterRatio(monstertype) { Ratio = currentRatio });
                }
            }

            if (cluster.Ratio1 != 0)
            {
                DbMonstertype monstertype = RoleManager.GetMonstertype(cluster.Monster1);
                if (monstertype == null)
                {
                    logger.LogWarning("Cluster {} has invalid monster type {}", cluster.Id, cluster.Monster1);
                }
                else
                {
                    currentRatio += cluster.Ratio1;
                    monsterRatios.Add(new MonsterRatio(monstertype) { Ratio = currentRatio });
                }
            }

            if (cluster.Ratio2 != 0)
            {
                DbMonstertype monstertype = RoleManager.GetMonstertype(cluster.Monster2);
                if (monstertype == null)
                {
                    logger.LogWarning("Cluster {} has invalid monster type {}", cluster.Id, cluster.Monster2);
                }
                else
                {
                    currentRatio += cluster.Ratio2;
                    monsterRatios.Add(new MonsterRatio(monstertype) { Ratio = currentRatio });
                }
            }

            if (cluster.Ratio3 != 0)
            {
                DbMonstertype monstertype = RoleManager.GetMonstertype(cluster.Monster3);
                if (monstertype == null)
                {
                    logger.LogWarning("Cluster {} has invalid monster type {}", cluster.Id, cluster.Monster3);
                }
                else
                {
                    currentRatio += cluster.Ratio3;
                    monsterRatios.Add(new MonsterRatio(monstertype) { Ratio = currentRatio });
                }
            }

            if (cluster.Ratio4 != 0)
            {
                DbMonstertype monstertype = RoleManager.GetMonstertype(cluster.Monster4);
                if (monstertype == null)
                {
                    logger.LogWarning("Cluster {} has invalid monster type {}", cluster.Id, cluster.Monster4);
                }
                else
                {
                    currentRatio += cluster.Ratio4;
                    monsterRatios.Add(new MonsterRatio(monstertype) { Ratio = currentRatio });
                }
            }

            if (cluster.Ratio5 != 0)
            {
                DbMonstertype monstertype = RoleManager.GetMonstertype(cluster.Monster5);
                if (monstertype == null)
                {
                    logger.LogWarning("Cluster {} has invalid monster type {}", cluster.Id, cluster.Monster5);
                }
                else
                {
                    currentRatio += cluster.Ratio5;
                    monsterRatios.Add(new MonsterRatio(monstertype) { Ratio = currentRatio });
                }
            }

            if (cluster.Ratio6 != 0)
            {
                DbMonstertype monstertype = RoleManager.GetMonstertype(cluster.Monster6);
                if (monstertype == null)
                {
                    logger.LogWarning("Cluster {} has invalid monster type {}", cluster.Id, cluster.Monster6);
                }
                else
                {
                    currentRatio += cluster.Ratio6;
                    monsterRatios.Add(new MonsterRatio(monstertype) { Ratio = currentRatio });
                }
            }

            if (cluster.Ratio7 != 0)
            {
                DbMonstertype monstertype = RoleManager.GetMonstertype(cluster.Monster7);
                if (monstertype == null)
                {
                    logger.LogWarning("Cluster {} has invalid monster type {}", cluster.Id, cluster.Monster7);
                }
                else
                {
                    currentRatio += cluster.Ratio7;
                    monsterRatios.Add(new MonsterRatio(monstertype) { Ratio = currentRatio });
                }
            }

            if (cluster.Ratio8 != 0)
            {
                DbMonstertype monstertype = RoleManager.GetMonstertype(cluster.Monster8);
                if (monstertype == null)
                {
                    logger.LogWarning("Cluster {} has invalid monster type {}", cluster.Id, cluster.Monster8);
                }
                else
                {
                    currentRatio += cluster.Ratio8;
                    monsterRatios.Add(new MonsterRatio(monstertype) { Ratio = currentRatio });
                }
            }

            if (cluster.Ratio9 != 0)
            {
                DbMonstertype monstertype = RoleManager.GetMonstertype(cluster.Monster9);
                if (monstertype == null)
                {
                    logger.LogWarning("Cluster {} has invalid monster type {}", cluster.Id, cluster.Monster9);
                }
                else
                {
                    currentRatio += cluster.Ratio9;
                    monsterRatios.Add(new MonsterRatio(monstertype) { Ratio = currentRatio });
                }
            }
        }

        public bool Add(Monster monster)
        {
            return monsters.TryAdd(monster.Identity, monster);
        }

        public async Task<Monster> GenerateMonsterAsync()
        {
            DbMonstertype selectedType = monstertype;
            if (HasCluster)
            {
                if (monsterRatios.Count == 0)
                {
                    return null;
                }

                int maxRatio = monsterRatios.Max(x => x.Ratio);
                int rate = maxRatio > 0 ? await NextAsync(maxRatio) : 0;
                selectedType = null;
                foreach (var ratio in monsterRatios)
                {
                    if (ratio.Ratio < rate)
                    {
                        selectedType = ratio.MonsterType;
                        break;
                    }
                }

                selectedType ??= monsterRatios.FirstOrDefault(x => x.MonsterType != null)?.MonsterType;
            }

            if (selectedType == null)
            {
                return null;
            }

            Point pos = await gameMap.QueryRandomPositionAsync(generator.BoundX, generator.BoundY, generator.BoundCx, generator.BoundCy);
            if (pos == default)
            {
                return null;
            }

            uint identity = (uint)IdentityManager.Monster.GetNextIdentity;
            if (identity == 0)
            {
                return null;
            }

            var monster = new Monster(selectedType, identity, this);
            if (!await monster.InitializeAsync(MapIdentity, (ushort)pos.X, (ushort)pos.Y))
            {
                IdentityManager.Monster.ReturnIdentity(monster.Identity);
                return null;
            }

            return monster;
        }

        public async Task<int> GenerateAsync()
        {
            foreach (var monster in awaitingReborn.Where(x => x.Value.IsTimeOut()))
            {
                if (awaitingReborn.TryRemove(monster.Key, out _))
                {
                    if (monsters.TryRemove(monster.Key, out _))
                    {
                        IdentityManager.Monster.ReturnIdentity(monster.Key);
                    }
                }
            }

            if (!IsActive)
            {
                return 0;
            }

            if (timeBetweenGens.ToNextTime())
            {
                int generate = Math.Min(generator.MaxPerGen - Generated, Math.Min(MAX_PER_GEN, generator.MaxNpc));
                if (generate > 0)
                {
                    while (generate-- > 0)
                    {
                        Monster monster = await GenerateMonsterAsync();
                        if (monster == null)
                        {
                            continue;
                        }

                        if (!monsters.TryAdd(monster.Identity, monster))
                        {
                            IdentityManager.Monster.ReturnIdentity(monster.Identity);
                            continue;
                        }

                        await monster.EnterMapAsync();
                    }
                }
            }

            return await OnTimerAsync();
        }

        public async Task<int> OnTimerAsync()
        {
            if (gameMap.IsRaceTrack())
            {
                return 0;
            }

            int count = 0;
            foreach (var monster in monsters.Values.Where(x => x.IsAlive && x.Map != null))
            {
                await monster.OnTimerAsync();
                count++;
            }
            return count;
        }

        public int SendAll()
        {
            var result = 0;
            MsgAiSpawnNpc msg = new();
            msg.Mode = AiSpawnNpcMode.Spawn;
            foreach (Monster npc in monsters.Values.Where(x => x.IsAlive && x.Map != null))
            {
                if (msg.List.Count >= 25)
                {
                    result += msg.List.Count;
                    BroadcastMsg(msg);
                    msg.List.Clear();
                }

                msg.List.Add(new MsgAiSpawnNpc<GameServer>.SpawnNpc
                {
                    Id = npc.Identity,
                    GeneratorId = Identity,
                    MonsterType = npc.Type,
                    MapId = npc.MapIdentity,
                    X = npc.X,
                    Y = npc.Y
                });
            }

            if (msg.List.Count > 0)
            {
                result += msg.List.Count;
                BroadcastMsg(msg);
            }

            return result;
        }

        public Point GetCenter()
        {
            return center;
        }

        public bool IsTooFar(ushort x, ushort y, int nRange)
        {
            return !(x >= generator.BoundX - nRange
                     && x < generator.BoundX + generator.BoundCx + nRange
                     && y >= generator.BoundY - nRange
                     && y < generator.BoundY + generator.BoundCy + nRange);
        }

        public bool IsInRegion(int x, int y)
        {
            return x >= generator.BoundX && x < generator.BoundX + generator.BoundCx
                                       && y >= generator.BoundY && y < generator.BoundY + generator.BoundCy;
        }

        public int GetWidth()
        {
            return generator.BoundCx;
        }

        public int GetHeight()
        {
            return generator.BoundCy;
        }

        public int GetPosX()
        {
            return generator.BoundX;
        }

        public int GetPosY()
        {
            return generator.BoundY;
        }

        public void Remove(uint role)
        {
            if (generator.MaxPerGen == 0)
            {
                if (monsters.TryRemove(role, out _))
                {
                    awaitingReborn.TryRemove(role, out _);
                    IdentityManager.Monster.ReturnIdentity(role);
                }
                return;
            }

            if (monsters.ContainsKey(role))
            {
                if (!awaitingReborn.ContainsKey(role))
                {
                    var tm = new TimeOut();
                    tm.Startup(RestSeconds);
                    awaitingReborn.TryAdd(role, tm);
                }
            }
            else
            {
                IdentityManager.Monster.ReturnIdentity(role);
            }
        }

        public override string ToString()
        {
            return $"[{MapIdentity}]{MonsterName}";
        }

        private const int MAX_PER_GEN = 20;
        private const int MIN_TIME_BETWEEN_GEN = 5;
    }
}
