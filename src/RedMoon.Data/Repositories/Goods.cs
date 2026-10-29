using System.Collections.Generic;
using RedMoon.Data.Entities;

namespace RedMoon.Data.Repositories
{
    public class GoodsRepository : HibernateDataRow<DbGoods>
    {
        public IList<DbGoods> FetchAll()
        {
            using (var pSession = SessionFactory.GameDatabase.OpenSession())
                return pSession
                    .CreateCriteria<DbGoods>()
                    .List<DbGoods>();
        }
    }
}