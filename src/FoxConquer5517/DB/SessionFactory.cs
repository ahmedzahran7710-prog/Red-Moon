// World Conquer / FoxConquer lineage, MIT; Red Moon local configuration repair.
using FluentNHibernate.Cfg;
using FluentNHibernate.Cfg.Db;
using MySql.Data.MySqlClient;
using NHibernate;
using ServerCore.Common;

namespace DB
{
    public sealed class SessionFactory
    {
        public static ISessionFactory LoginDatabase;
        public static ISessionFactory GameDatabase;
        public static ISessionFactory EventDatabase;

        public SessionFactory(string file, bool isLogin)
        {
            var factory = Create(RuntimeSettings.Load(file));
            if (isLogin) LoginDatabase = factory; else GameDatabase = factory;
        }
        public SessionFactory(string game, string login)
        {
            GameDatabase = Create(RuntimeSettings.Load(game));
            LoginDatabase = Create(RuntimeSettings.Load(login));
        }
        private static ISessionFactory Create(RuntimeSettings config)
        {
            // A builder correctly escapes delimiters in local credentials. No root
            // password is baked into source, a command line, or the distributed INIs.
            string host = config.Get("MySQL", "Hostname");
            if (host != "127.0.0.1" && host != "localhost")
                throw new System.InvalidOperationException("This local setup only permits loopback database connections.");
            var connection = new MySqlConnectionStringBuilder
            {
                Server = config.Get("MySQL", "Hostname"),
                Port = (uint)config.Port("MySQL", "Port", 3306),
                Database = config.Get("MySQL", "Database"),
                UserID = config.Get("MySQL", "Username"),
                Password = config.Get("MySQL", "Password"),
                CharacterSet = "utf8mb4",
                ConnectionTimeout = 10,
                DefaultCommandTimeout = 30,
                Pooling = true,
                AllowZeroDateTime = true,
                ConvertZeroDateTime = true,
                // Prepared local setup only allows a loopback database endpoint.
                SslMode = MySqlSslMode.Disabled
            };
            var session = Fluently.Configure()
                .Database(MySQLConfiguration.Standard.ConnectionString(connection.ConnectionString))
                .Mappings(m => m.FluentMappings.AddFromAssemblyOf<SessionFactory>());
            session.ExposeConfiguration(x => x.SetProperty("hbm2ddl.keywords", "auto-quote"));
            return session.BuildSessionFactory(); // No automatic schema migration/drop.
        }
    }
}
