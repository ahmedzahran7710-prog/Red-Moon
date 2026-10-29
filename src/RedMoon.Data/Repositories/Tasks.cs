using System.Collections.Generic;
using RedMoon.Data.Entities;

namespace RedMoon.Data.Repositories
{
    public class TasksRespository : HibernateDataRow<DbTask>
    {
        public IList<DbTask> FetchAll()
        {
            using (var pSession = SessionFactory.GameDatabase.OpenSession())
                return pSession
                    .CreateCriteria<DbTask>()
                    .List<DbTask>();
        }
    }
}
