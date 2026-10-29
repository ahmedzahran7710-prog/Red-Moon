using System.Collections.Generic;
using RedMoon.Data.Entities;

namespace RedMoon.Data.Repositories
{
    public class SyndicateRepository : HibernateDataRow<DbSyndicate>
    {
        public IList<DbSyndicate> FetchAll()
        {
            using (var pSession = SessionFactory.GameDatabase.OpenSession())
                return pSession
                    .CreateCriteria<DbSyndicate>()
                    .List<DbSyndicate>();
        }

        public bool SaveOrUpdate(DbSyndicate obj)
        {
            return SaveOrUpdate(obj, SessionFactory.GameDatabase.OpenSession());
        }

        public bool Delete(DbSyndicate obj)
        {
            return TryDelete(obj, SessionFactory.GameDatabase.OpenSession());
        }
    }
}