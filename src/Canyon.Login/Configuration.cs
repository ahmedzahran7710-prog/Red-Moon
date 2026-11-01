using Canyon.Database;
using Microsoft.Extensions.Configuration;

namespace Canyon.Login
{
    /// <summary>
    ///     Defines the configuration file structure for the Account Server. App Configuration
    ///     files are copied to the build output directory on successful build, containing all
    ///     default configuration settings for the server, only if the file is newer than the
    ///     file bring replaced.
    /// </summary>
    public class ServerConfiguration
    {
        /// <summary>
        ///     Instantiates a new instance of <see cref="ServerConfiguration" /> with command-line
        ///     arguments from the user and a configuration file for the application. Builds the
        ///     configuration file and binds to this instance of the ServerConfiguration class.
        /// </summary>
        public ServerConfiguration(params string[] args)
        {
            bool isDocker = Environment.GetEnvironmentVariable("MACHINE_ENV") == "docker";
            var legacyConfigFile = isDocker ? "Canyon.Login.Config.json" : "Canyon.Login.Config.local.json";
            var configFile = isDocker ? "MoonQueen.Login.Config.json" : "MoonQueen.Login.Config.local.json";

            new ConfigurationBuilder()
                .AddJsonFile(legacyConfigFile, optional: true)
                .AddJsonFile(configFile, optional: true)
                .AddCommandLine(args)
                .AddEnvironmentVariables()
                .Build()
                .Bind(this);
        }

        // Properties and fields
        public AuthAesConfiguration Auth { get; set; }
        public NetworkConfiguration Network { get; set; }
        public NetworkConfiguration RealmNetwork { get; set; }
        public AuthenticationConfiguration Authentication { get; set; }
        public RealmConfiguration Realm { get; set; }
        public AccountConfiguration Account { get; set; }
        public DatabaseConfiguration Database { get; set; }


        /// <summary>
        ///     Returns true if the server configuration is valid after reading.
        /// </summary>
        public bool Valid =>
            Auth != null
            && !string.IsNullOrEmpty(Auth.SharedKey)
            && !string.IsNullOrEmpty(Auth.SharedIV)
            && Network != null
            && RealmNetwork != null
#if !USE_MYSQL_DB
            && Authentication != null
            && Realm != null
            && Account != null
#endif
            ;

        public class AuthAesConfiguration
        {
            public string SharedKey { get; set; }
            public string SharedIV { get; set; }
        }

        /// <summary>
        ///     Encapsulates network configuration for the server listener.
        /// </summary>
        public class NetworkConfiguration
        {
            public string IPAddress { get; set; }
            public int Port { get; set; }
            public int MaxConn { get; set; }
        }

        public class AuthenticationConfiguration
        {
            public string Identity { get; set; }
            public string Url { get; set; }
            public string ClientId { get; set; }
            public string ClientSecret { get; set; }
            public string Scope { get; set; }
        }

        public class RealmConfiguration
        {
            public string Url { get; set; }
        }

        public class AccountConfiguration
        {
            public string Url { get; set; }
        }
    }
}
