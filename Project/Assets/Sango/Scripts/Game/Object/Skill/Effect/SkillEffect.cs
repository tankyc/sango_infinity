using TKNewtonsoft.Json;
using TKNewtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core
{
    public abstract class SkillEffect
    {
        public struct SkillEffectConditionDatabase : IConditionDatabase
        {
            Cell atk_cell;
            SkillEffect self;
            public SkillEffectConditionDatabase(SkillEffect self, Cell atk_cell)
            {
                this.atk_cell = atk_cell;
                this.self = self;
            }

            public SkillInstance ActionSkill => self.master;
            public SkillInstance TargetSkill => null;
            public Person ActionPerson => self.master.master.Leader;
            public Person TargetPerson => atk_cell.troop?.Leader;
            public Troop ActionTroop => self.master.master;
            public Troop TargetTroop => atk_cell.troop;
            public Cell ActionCell => self.master.master.cell;
            public Cell TargetCell => atk_cell;
            public City ActionCity => self.master.master.BelongCity;
            public City TargetCity => atk_cell.troop?.BelongCity ?? atk_cell.building?.BelongCity;
            public Corps ActionCorps => self.master.master.BelongCorps;
            public Corps TargetCorps => atk_cell.troop?.BelongCorps ?? atk_cell.building?.BelongCorps;
            public Force ActionForce => self.master.master.BelongForce;
            public Force TargetForce => atk_cell.troop?.BelongForce ?? atk_cell.building?.BelongForce;
            public Fire ActiveFire => self.master.master.cell.fire;
            public Fire TargetFire => atk_cell.fire;
            public object ActionObject => self;
            public object TargetObject => atk_cell;
        }

        public SkillInstance master;
        public virtual void Init(JObject p, SkillInstance master) { this.master = master; }
        public abstract void Action(Cell targetCell);
        public virtual void Clear() {}

        public delegate SkillEffect SkillEffectCreator();

        public static Dictionary<string, SkillEffectCreator> CreateMap = new Dictionary<string, SkillEffectCreator>();
        public static void Register(string name, SkillEffectCreator action)
        {
            CreateMap[name] = action;
        }
        public static SkillEffect CraeteHandle<T>() where T : SkillEffect, new()
        {
            return new T();
        }
        public static SkillEffect Create(string name)
        {
            SkillEffectCreator creator;
            if (CreateMap.TryGetValue(name, out creator))
                return creator();
            return null;
        }

        public static void Init()
        {

            Register("SetFire", CraeteHandle<SetFire>);
            Register("AddBuff", CraeteHandle<AddBuff>);
            Register("RemoveBuffById", CraeteHandle<RemoveBuffById>);
            Register("RemoveBuffByKind", CraeteHandle<RemoveBuffByKind>);
            Register("PutoutFire", CraeteHandle<PutoutFire>);
            Register("RecureWoundedTroops", CraeteHandle<RecureWoundedTroops>);

        }





    }
}
