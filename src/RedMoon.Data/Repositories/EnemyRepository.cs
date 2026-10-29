using System.Collections.Generic;
using RedMoon.Data.Entities;
using NHibernate.Criterion;

namespace RedMoon.Data.Repositories
{
    public class EnemyRepository : HibernateDataRow<DbEnemy>
    {
        public IList<DbEnemy> GetUserEnemies(uint userId)
        {
            using (var sessionFactory = SessionFactory.GameDatabase.OpenSession())
                return sessionFactory
                    .CreateCriteria<DbEnemy>()
                    .Add(Restrictions.Eq("UserIdentity", userId))
                    .SetMaxResults(100)
                    .List<DbEnemy>();
        }

        public bool SaveOrUpdate(DbEnemy obj)
        {
            return SaveOrUpdate(obj, SessionFactory.GameDatabase.OpenSession());
        }

        public bool Delete(DbEnemy obj)
        {
            return TryDelete(obj, SessionFactory.GameDatabase.OpenSession());
        }
    }
}