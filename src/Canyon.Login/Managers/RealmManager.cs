using Canyon.Login.States;
using Canyon.Shared;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Canyon.Login.Managers
{
    public class RealmManager
    {
        private static readonly ILogger logger = LogFactory.CreateLogger<RealmManager>();

        private static readonly ConcurrentDictionary<Guid, Realm> Realms = new();

        private RealmManager() { }

        public static int Count => Realms.Count;

        public static bool AddRealm(Realm realm)
        {
            if (realm == null || realm.RealmID == Guid.Empty)
            {
                return false;
            }

            Realms[realm.RealmID] = realm;
            return true;
        }

        public static bool HasRealm(Guid idRealm) => Realms.ContainsKey(idRealm);

        public static Realm GetRealm(string name) => Realms.Values.FirstOrDefault(x => x.Data?.RealmName != null && x.Data.RealmName.Equals(name));
        public static Realm GetRealm(Guid idRealm) => Realms.TryGetValue(idRealm, out var result) ? result : null;

        public static bool RemoveRealm(Guid idRealm)
        {
            return Realms.TryRemove(idRealm, out _);
        }

        public static bool RemoveRealm(Guid idRealm, Realm instance)
        {
            if (instance == null)
            {
                return Realms.TryRemove(idRealm, out _);
            }

            return Realms.TryRemove(KeyValuePair.Create(idRealm, instance));
        }
    }
}
