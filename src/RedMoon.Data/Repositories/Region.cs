using System.Collections.Generic;
using RedMoon.Data.Entities;

namespace RedMoon.Data.Repositories
{
    public class MapRegion : HibernateDataRow<DbRegion>
    {
        public IList<DbRegion> FetchAll()
        {
            using (var pSession = SessionFactory.GameDatabase.OpenSession())
                return pSession
                    .CreateCriteria<DbRegion>()
                    .List<DbRegion>();
        }
    }
}