using Microsoft.Extensions.Configuration;

namespace Canyon.Game.Scripting
{
    public class LuaScriptsSettings
    {
        public static LuaScriptsSettings Settings { get; private set; }

        static LuaScriptsSettings()
        {
            Reload();
        }

        public static void Reload()
        {
            Settings = new LuaScriptsSettings();
        }

        private static readonly ILogger logger = LogFactory.CreateLogger<LuaScriptsSettings>();

        public LuaScriptsSettings() 
        {
            string luaIniPath = Path.Combine(Environment.CurrentDirectory, "lua", "lua.ini");
            if (!File.Exists(luaIniPath))
            {
                logger.LogWarning("Lua index file lua/lua.ini not found at {Path}; copy RequiredAssetsForRun/lua into {Dir}/lua.", luaIniPath, Environment.CurrentDirectory);
                return;
            }

            new ConfigurationBuilder()
                .AddIniFile(luaIniPath, optional: true)
                .Build()
                .Bind(this);
        }

        public Dictionary<string, string> MOD { get; set; } = new();
    }
}
