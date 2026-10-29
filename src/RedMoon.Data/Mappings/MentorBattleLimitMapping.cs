using RedMoon.Data.Entities;
using FluentNHibernate.Mapping;

namespace RedMoon.Data.Mappings
{
    public class MentorBattleLimitMapping : ClassMap<MentorBattleLimit>
    {
        public MentorBattleLimitMapping()
        {
            Table(TableName.TUTOR_BATTLE_LIMIT_TYPE);
            LazyLoad();
            Id(x => x.Id).GeneratedBy.Identity().Not.Nullable().Column("id");
            Map(x => x.BattleLevelLimit).Default("0").Not.Nullable().Column("Battle_lev_limit");
        }
    }
}