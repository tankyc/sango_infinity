using Sango.Game.Battle.Skill;

namespace Sango.Game.Battle.Buff
{
    public class BattleBuffEntityList
    {
        public BattleBuffEntity[] entities;
        public BattleBuffEntityList Clone()
        {
            BattleBuffEntityList battleBuffEntityList = new BattleBuffEntityList();
            BattleBuffEntity[] battleBuffEntities = new BattleBuffEntity[entities.Length];
            for (int i = 0; i < battleBuffEntities.Length; ++i)
                battleBuffEntities[i] = entities[i].Clone();
            battleBuffEntityList.entities = battleBuffEntities;
            return battleBuffEntityList;
        }

        public void SetOwner(BattleBuff buff)
        {
            for (int i = 0; i < entities.Length; ++i)
                entities[i].SetOwner(buff);
        }

        public void Active()
        {
            for (int i = 0; i < entities.Length; ++i)
                entities[i].Active();
        }

        public void Clear()
        {
            for (int i = 0; i < entities.Length; ++i)
                entities[i].Clear();
        }
    }
}