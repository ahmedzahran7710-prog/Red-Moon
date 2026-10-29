using System.Collections.Generic;
using RedMoon.Data.Entities;

namespace RedMoon.Data.Repositories
{
    public class MagictypeOpRepository : HibernateDataRow<DbMagictypeop>
    {
        public IList<DbMagictypeop> FetchAll()
        {
            using (var pSession = SessionFactory.GameDatabase.OpenSession())
                return pSession
                    .CreateCriteria<DbMagictypeop>()
                    .List<DbMagictypeop>();
        }
    }
}