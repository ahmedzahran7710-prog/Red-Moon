using System.Collections.Generic;
using RedMoon.Data.Entities;

namespace RedMoon.Data.Repositories
{
    public class NobilityRepository : HibernateDataRow<DbDynaRankRec>
    {
        public IList<DbDynaRankRec> FetchAll()
        {
            using (var pSession = SessionFactory.GameDatabase.OpenSession())
                return pSession
                    .CreateCriteria<DbDynaRankRec>()
                    .List<DbDynaRankRec>();
        }

        public bool SaveOrUpdate(DbDynaRankRec obj)
        {
            return base.SaveOrUpdate(obj, SessionFactory.GameDatabase.OpenSession());
        }

        public bool Delete(DbDynaRankRec obj)
        {
            return base.TryDelete(obj, SessionFactory.GameDatabase.OpenSession());
        }
    }
}