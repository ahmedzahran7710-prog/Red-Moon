using System.Collections.Generic;
using RedMoon.Data.Entities;

namespace RedMoon.Data.Repositories
{
    public class MentorTypeRepository : HibernateDataRow<DbMentorType>
    {
        public IList<DbMentorType> FetchAll()
        {
            using (var pSession = SessionFactory.GameDatabase.OpenSession())
                return pSession
                    .CreateCriteria<DbMentorType>()
                    .List<DbMentorType>();
        }
    }
}