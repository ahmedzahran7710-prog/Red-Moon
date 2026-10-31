using Canyon.Database;
using Microsoft.Extensions.Configuration;

namespace Canyon.Ai
{
    public class ServerConfiguration
    {
        public static ServerConfiguration Configuration { get; set; }

        public ServerConfiguration(params string[] args)
        {
            bool isDocker = Environment.GetEnvironmentVariable("MACHINE_ENV") == "docker";
            var legacyConfigFile = isDocker ? "Canyon.Ai.Config.json" : "Canyon.Ai.Config.local.json";
            var configFile = isDocker ? "MoonQueen.Ai.Config.json" : "MoonQueen.Ai.Config.local.json";

            new ConfigurationBuilder()
                .AddJsonFile(legacyConfigFile, optional: true)
                .AddJsonFile(configFile, optional: true)
                .AddCommandLine(args)
                .AddEnvironmentVariables()
                .Build()
                .Bind(this);
        }

        public DatabaseConfiguration Database { get; set; }
        public RealmConfiguration Ai { get; set; }

        public class RealmConfiguration
        {
            public Guid ServerId { get; set; }
            public string Name { get; set; }

            public string IPAddress { get; set; }
            public int Port { get; set; }

            public string Username { get; set; }
            public string Password { get; set; }
        }
    }
}
