using System.Collections.Generic;
using RedMoon.Data.Entities;

namespace RedMoon.Data.Repositories
{
    public class PasswayRepository : HibernateDataRow<DbPassway>
    {
        public IList<DbPassway> GetAllPassways()
        {
            using (var sessionFactory = SessionFactory.GameDatabase.OpenSession())
            {
                return sessionFactory
                    .CreateCriteria<DbPassway>()
                    .List<DbPassway>();
            }
        }
    }
}