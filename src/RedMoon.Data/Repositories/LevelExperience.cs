using System.Collections.Generic;
using RedMoon.Data.Entities;

namespace RedMoon.Data.Repositories
{
    public class LevelExperience : HibernateDataRow<DbLevexp>
    {
        public IList<DbLevexp> FetchAll()
        {
            using (var pSession = SessionFactory.GameDatabase.OpenSession())
                return pSession
                    .CreateCriteria<DbLevexp>()
                    .List<DbLevexp>();
        }
    }
}