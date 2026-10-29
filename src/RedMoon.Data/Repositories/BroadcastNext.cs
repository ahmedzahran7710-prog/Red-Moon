using System.Collections.Generic;
using RedMoon.Data.Entities;
using NHibernate.Criterion;

namespace RedMoon.Data.Repositories
{
    public class BroadcastQueueRepository : HibernateDataRow<DbBroadcastQueue>
    {
        public IList<DbBroadcastQueue> FetchByUser(uint dwId)
        {
            using (var pSession = SessionFactory.GameDatabase.OpenSession())
                return pSession.CreateCriteria<DbBroadcastQueue>()
                    .Add(Restrictions.Eq("UserIdentity", dwId))
                    .SetMaxResults(10)
                    .List<DbBroadcastQueue>();
        }

        public IList<DbBroadcastQueue> FetchAll()
        {
            using (var pSession = SessionFactory.GameDatabase.OpenSession())
                return pSession.CreateCriteria<DbBroadcastQueue>()
                    .List<DbBroadcastQueue>();
        }

        public bool SaveOrUpdate(DbBroadcastQueue obj)
        {
            return SaveOrUpdate(obj, SessionFactory.GameDatabase.OpenSession());
        }

        public bool Delete(DbBroadcastQueue obj)
        {
            return TryDelete(obj, SessionFactory.GameDatabase.OpenSession());
        }
    }
}
