using Canyon.Database;
using Microsoft.Extensions.Configuration;

namespace Canyon.Game
{
    public class ServerConfiguration
    {
        public static ServerConfiguration Configuration { get; set; }

        public ServerConfiguration(params string[] args)
        {
            bool isDocker = Environment.GetEnvironmentVariable("MACHINE_ENV") == "docker";
            var legacyConfigFile = isDocker ? "Canyon.Game.Config.json" : "Canyon.Game.Config.local.json";
            var configFile = isDocker ? "MoonQueen.Game.Config.json" : "MoonQueen.Game.Config.local.json";

            new ConfigurationBuilder()
                .AddJsonFile(legacyConfigFile, optional: true)
                .AddJsonFile(configFile, optional: true)
                .AddCommandLine(args)
                .AddEnvironmentVariables()
                .Build()
                .Bind(this);
        }

        public AuthAesConfiguration Auth { get; set; }
        public DatabaseConfiguration Database { get; set; }
        public RealmConfiguration Realm { get; set; }
        public LoginConfiguration Login { get; set; }
        public AiConfiguration Ai { get; set; }
        public List<MaintenanceScheduleConfiguration> MaintenanceSchedule { get; set; }
        public PigletConfiguration Piglet { get; set; }
        public ServerRatesConfiguration Rates { get; set; } = new();
        public bool CooperatorMode { get; set; } = false;

        public class ServerRatesConfiguration
        {
            public string PresetName { get; set; } = "Pure Official Conquer (1x)";
            public double ExperienceRate { get; set; } = 1.0d;
            public double MagicExpRate { get; set; } = 1.0d;
            public double WeaponSkillExpRate { get; set; } = 1.0d;
            public double SilverDropRate { get; set; } = 1.0d;
            public int SilverDropChancePercent { get; set; } = 25;
            public double EquipmentDropRate { get; set; } = 1.0d;
            public double QualityItemRate { get; set; } = 1.0d;
            public double SocketDropRate { get; set; } = 1.0d;
            public double PlusOneDropRate { get; set; } = 1.0d;
            public double DragonBallDropRate { get; set; } = 1.0d;
            public int MeteorDropPer100k { get; set; } = 0;
            public double MonsterCpChancePercent { get; set; } = 0.0d;
            public int MonsterCpMin { get; set; } = 1;
            public int MonsterCpMax { get; set; } = 3;
            public string BroadcastMessage { get; set; } = string.Empty;
            public string BroadcastChannel { get; set; } = "TopLeft";
            public long BroadcastStamp { get; set; } = 0;
            public long ReloadActionsStamp { get; set; } = 0;

            public ServerRatesConfiguration CloneNormalized()
            {
                int cpMin = Math.Clamp(MonsterCpMin, 1, 100_000);
                int cpMax = Math.Clamp(MonsterCpMax, cpMin, 100_000);
                return new ServerRatesConfiguration
                {
                    PresetName = string.IsNullOrWhiteSpace(PresetName) ? "Pure Official Conquer (1x)" : PresetName.Trim(),
                    ExperienceRate = Math.Clamp(double.IsFinite(ExperienceRate) ? ExperienceRate : 1.0d, 0.05d, 100.0d),
                    MagicExpRate = Math.Clamp(double.IsFinite(MagicExpRate) ? MagicExpRate : 1.0d, 0.05d, 100.0d),
                    WeaponSkillExpRate = Math.Clamp(double.IsFinite(WeaponSkillExpRate) ? WeaponSkillExpRate : 1.0d, 0.05d, 100.0d),
                    SilverDropRate = Math.Clamp(double.IsFinite(SilverDropRate) ? SilverDropRate : 1.0d, 0.0d, 100.0d),
                    SilverDropChancePercent = Math.Clamp(SilverDropChancePercent, 0, 100),
                    EquipmentDropRate = Math.Clamp(double.IsFinite(EquipmentDropRate) ? EquipmentDropRate : 1.0d, 0.0d, 25.0d),
                    QualityItemRate = Math.Clamp(double.IsFinite(QualityItemRate) ? QualityItemRate : 1.0d, 0.0d, 50.0d),
                    SocketDropRate = Math.Clamp(double.IsFinite(SocketDropRate) ? SocketDropRate : 1.0d, 0.0d, 100.0d),
                    PlusOneDropRate = Math.Clamp(double.IsFinite(PlusOneDropRate) ? PlusOneDropRate : 1.0d, 0.0d, 100.0d),
                    DragonBallDropRate = Math.Clamp(double.IsFinite(DragonBallDropRate) ? DragonBallDropRate : 1.0d, 0.0d, 1000.0d),
                    MeteorDropPer100k = Math.Clamp(MeteorDropPer100k, 0, 100_000),
                    MonsterCpChancePercent = Math.Clamp(double.IsFinite(MonsterCpChancePercent) ? MonsterCpChancePercent : 0.0d, 0.0d, 100.0d),
                    MonsterCpMin = cpMin,
                    MonsterCpMax = cpMax,
                    BroadcastMessage = (BroadcastMessage ?? string.Empty).Trim(),
                    BroadcastChannel = string.IsNullOrWhiteSpace(BroadcastChannel) ? "TopLeft" : BroadcastChannel.Trim(),
                    BroadcastStamp = BroadcastStamp,
                    ReloadActionsStamp = ReloadActionsStamp
                };
            }
        }

        public class AuthAesConfiguration
        {
            public string SharedKey { get; set; }
            public string SharedIV { get; set; }
        }

        public class RealmConfiguration
        {
            public Guid ServerId { get; set; }
            public string Name { get; set; }

            public string IPAddress { get; set; }
            public int Port { get; set; }

            public int MaxOnlinePlayers { get; set; }

            public string Username { get; set; }
            public string Password { get; set; }

            public DateTime ReleaseDate { get; set; }

            public int Processors { get; set; } = 1;
        }

        public class LoginConfiguration
        {
            public string IPAddress { get; set; }
            public int Port { get; set; }
        }

        public class AiConfiguration
        {
            public string IPAddress { get; set; }
            public int Port { get; set; }

            public string Username { get; set; }
            public string Password { get; set; }
        }

        public class MaintenanceScheduleConfiguration
        {
            public DayOfWeek DayOfWeek { get; set; }
            public string Time { get; set; }
            public int WarningMinutes { get; set; }
        }

        public class PigletConfiguration
        {
            public string IPAddress { get; set; }
            public int Port { get; set; }

            public string Username { get; set; }
            public string Password { get; set; }
        }
    }
}
