using System.Collections.Generic;
using RedMoon.Data.Entities;

namespace RedMoon.Data.Repositories
{
    public class MonstersRepository : HibernateDataRow<DbMonstertype>
    {
        public IList<DbMonstertype> FetchAll()
        {
            using (var pSession = SessionFactory.GameDatabase.OpenSession())
                return pSession
                    .CreateCriteria<DbMonstertype>()
                    .List<DbMonstertype>();
        }
    }
}