using System.Collections.Generic;
using RedMoon.Data.Entities;
using NHibernate.Criterion;

namespace RedMoon.Data.Repositories
{
    public class StatisticRepository : HibernateDataRow<DbStatistic>
    {
        public IList<DbStatistic> FetchList(uint owner)
        {
            using (var sessionFactory = SessionFactory.GameDatabase.OpenSession())
                return sessionFactory
                    .CreateCriteria<DbStatistic>()
                    .Add(Restrictions.Eq("PlayerIdentity", owner))
                    .List<DbStatistic>();
        }

        public bool SaveOrUpdate(DbStatistic obj)
        {
            return base.SaveOrUpdate(obj, SessionFactory.GameDatabase.OpenSession());
        }

        public bool Delete(DbStatistic obj)
        {
            return base.TryDelete(obj, SessionFactory.GameDatabase.OpenSession());
        }
    }
}
