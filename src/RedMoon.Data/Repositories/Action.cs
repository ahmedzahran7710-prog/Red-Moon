using System.Collections.Generic;
using RedMoon.Data.Entities;
using NHibernate.Criterion;

namespace RedMoon.Data.Repositories
{
    public class GameActionRepo : HibernateDataRow<DbGameAction>
    {
        public IList<DbGameAction> FetchAll()
        {
            using (var pSession = SessionFactory.GameDatabase.OpenSession())
                return pSession
                    .CreateCriteria<DbGameAction>()
                    .List<DbGameAction>();
        }

        public DbGameAction GetById(uint id)
        {
            using (var pSession = SessionFactory.GameDatabase.OpenSession())
                return pSession
                    .CreateCriteria<DbGameAction>()
                    .Add(Restrictions.Eq("Identity", id))
                    .SetMaxResults(1)
                    .UniqueResult<DbGameAction>();
        }
    }
}