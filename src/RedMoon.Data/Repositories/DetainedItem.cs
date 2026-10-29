using System.Collections.Generic;
using RedMoon.Data.Entities;

namespace RedMoon.Data.Repositories
{
    public class DetainedItemRepository : HibernateDataRow<DbDetainedItem>
    {
        public IList<DbDetainedItem> FetchAll()
        {
            using (var pSession = SessionFactory.GameDatabase.OpenSession())
                return pSession
                    .CreateCriteria<DbDetainedItem>()
                    .List<DbDetainedItem>();
        }

        public bool SaveOrUpdate(DbDetainedItem obj)
        {
            return SaveOrUpdate(obj, SessionFactory.GameDatabase.OpenSession());
        }

        public bool Delete(DbDetainedItem obj)
        {
            return TryDelete(obj, SessionFactory.GameDatabase.OpenSession());
        }
    }
}