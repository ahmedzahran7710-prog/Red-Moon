using System.Collections.Generic;
using RedMoon.Data.Entities;

namespace RedMoon.Data.Repositories
{
    public class MonsterMagicRepository : HibernateDataRow<DbMonsterMagic>
    {
        public IList<DbMonsterMagic> FetchAll()
        {
            using (var pSession = SessionFactory.GameDatabase.OpenSession())
                return pSession
                    .CreateCriteria<DbMonsterMagic>()
                    .List<DbMonsterMagic>();
        }
    }
}