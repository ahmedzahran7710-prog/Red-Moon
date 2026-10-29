using System.Collections.Generic;
using RedMoon.Data.Entities;

namespace RedMoon.Data.Repositories
{
    public class MagicTypeRepository : HibernateDataRow<DbMagictype>
    {
        public IList<DbMagictype> FetchAll()
        {
            using (var pSession = SessionFactory.GameDatabase.OpenSession())
                return pSession
                    .CreateCriteria<DbMagictype>()
                    .List<DbMagictype>();
        }
    }
}