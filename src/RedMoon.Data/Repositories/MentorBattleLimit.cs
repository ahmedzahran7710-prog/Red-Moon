using System.Collections.Generic;
using RedMoon.Data.Entities;

namespace RedMoon.Data.Repositories
{
    public class MentorBattleLimitRepository : HibernateDataRow<MentorBattleLimit>
    {
        public IList<MentorBattleLimit> FetchAll()
        {
            using (var pSession = SessionFactory.GameDatabase.OpenSession())
                return pSession
                    .CreateCriteria<MentorBattleLimit>()
                    .List<MentorBattleLimit>();
        }
    }
}