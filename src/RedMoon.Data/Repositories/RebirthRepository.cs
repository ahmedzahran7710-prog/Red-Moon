using System.Collections.Generic;
using RedMoon.Data.Entities;

namespace RedMoon.Data.Repositories
{
    public class RebirthRepository : HibernateDataRow<DbCqRebirth>
    {
        public IList<DbCqRebirth> FetchAll()
        {
            using (var pSession = SessionFactory.GameDatabase.OpenSession())
                return pSession
                    .CreateCriteria<DbCqRebirth>()
                    .List<DbCqRebirth>();
        }
    }
}
