using TKNewtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core.Action
{
    public abstract class TroopActionBase : ActionBase
    {
        public struct TroopActionConditionDatabase : IConditionDatabase
        {
            Cell atk_cell;
            SkillInstance self;
            public TroopActionConditionDatabase(SkillInstance self, Cell atk_cell)
            {
                this.atk_cell = atk_cell;
                this.self = self;
            }

            public SkillInstance ActionSkill => self;
            public SkillInstance TargetSkill => null;
            public Person ActionPerson => self.master.Leader;
            public Person TargetPerson => atk_cell.troop?.Leader;
            public Troop ActionTroop => self.master;
            public Troop TargetTroop => atk_cell.troop;
            public Cell ActionCell => self.master.cell;
            public Cell TargetCell => atk_cell;
            public City ActionCity => self.master.mBelongCity;
            public City TargetCity => atk_cell.troop?.mBelongCity ?? atk_cell.building?.mBelongCity;
            public Corps ActionCorps => self.master.mBelongCorps;
            public Corps TargetCorps => atk_cell.troop?.mBelongCorps ?? atk_cell.building?.mBelongCorps;
            public Force ActionForce => self.master.mBelongForce;
            public Force TargetForce => atk_cell.troop?.mBelongForce ?? atk_cell.building?.mBelongForce;

            public Fire ActiveFire => self.master.cell.fire;
            public Fire TargetFire => atk_cell.fire;
            public object ActionObject => self;
            public object TargetObject => atk_cell;
        }

        public struct TroopSkillConditionDatabase : IConditionDatabase
        {
            SkillInstance self;
            public TroopSkillConditionDatabase(SkillInstance self)
            {
                this.self = self;
            }

            public SkillInstance ActionSkill => self;
            public SkillInstance TargetSkill => null;
            public Person ActionPerson => self.master.Leader;
            public Person TargetPerson => null;
            public Troop ActionTroop => self.master;
            public Troop TargetTroop => null;
            public Cell ActionCell => self.master.cell;
            public Cell TargetCell => null;
            public City ActionCity => self.master.mBelongCity;
            public City TargetCity => null;
            public Corps ActionCorps => self.master.mBelongCorps;
            public Corps TargetCorps => null;
            public Force ActionForce => self.master.mBelongForce;
            public Force TargetForce => null;

            public Fire ActiveFire => null;
            public Fire TargetFire => null;
            public object ActionObject => self;
            public object TargetObject => null;
        }

        public struct TroopConditionDatabase : IConditionDatabase
        {
            Troop self;
            public TroopConditionDatabase(Troop self)
            {
                this.self = self;
            }

            public SkillInstance ActionSkill => null;
            public SkillInstance TargetSkill => null;
            public Person ActionPerson => self.Leader;
            public Person TargetPerson => null;
            public Troop ActionTroop => self;
            public Troop TargetTroop => null;
            public Cell ActionCell => null;
            public Cell TargetCell => null;
            public City ActionCity => self.mBelongCity;
            public City TargetCity => null;
            public Corps ActionCorps => self.mBelongCorps;
            public Corps TargetCorps => null;
            public Force ActionForce => self.mBelongForce;
            public Force TargetForce => null;

            public Fire ActiveFire => null;
            public Fire TargetFire => null;
            public object ActionObject => self;
            public object TargetObject => null;
        }

        protected Force Force { get; set; }
        protected Troop Troop { get; set; }
        protected JObject Params { get; set; }

        protected int value;
        protected List<int> kinds;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            Troop = sangoObjects[0] as Troop;
            if(Troop == null)
                Force = sangoObjects[0] as Force;
            Params = p;
            value = p.Value<int>("value");
            JArray kindsArray = p.Value<JArray>("kinds");
            if (kindsArray != null)
            {
                kinds = new List<int>(kindsArray.Count);
                for (int i = 0; i < kindsArray.Count; i++)
                    kinds.Add(kindsArray[i].ToObject<int>());
            }
        }

        /// <summary>
        /// 兵种类型(0全兵种全地形 -1陆地 -2水上)
        /// </summary>
        /// <param name="troop"></param>
        /// <param name="checkTroopTypeKind"></param>
        /// <returns></returns>
        public bool CheckTroopTypeKind(Troop troop, int checkTroopTypeKind)
        {
            if (checkTroopTypeKind == -1 && troop.IsInWater)
                return false;
            if (checkTroopTypeKind == -2 && !troop.IsInWater)
                return false;

            if (checkTroopTypeKind > 0 && ((troop.LandTroopType.kind == checkTroopTypeKind && troop.IsInWater) || (troop.WaterTroopType.kind == checkTroopTypeKind && !troop.IsInWater)))
                return false;
            return true;
        }

        /// <summary>
        /// 是否一般攻击 1一般攻击 2非一般攻击 0都可以
        /// </summary>
        /// <param name="skill"></param>
        /// <param name="isNormalSkill"></param>
        /// <returns></returns>
        public bool CheckIsNormalSkill(SkillInstance skill, int isNormalSkill)
        {
            if ((isNormalSkill == 1 && !skill.IsNormal()) || (isNormalSkill == 2 && skill.IsNormal()))
                return false;
            return true;
        }

        /// <summary>
        /// 是否是远程 1远程 2近战 0都可以 
        /// </summary>
        /// <param name="skill"></param>
        /// <param name="isRangeSkill"></param>
        /// <returns></returns>
        public bool CheckIsRangeSkill(SkillInstance skill, int isRangeSkill)
        {
            if ((isRangeSkill == 1 && !skill.IsRange()) || (isRangeSkill == 2 && skill.IsRange()))
                return false;
            return true;
        }

        public override void Execute(Trigger trigger, params object[] sangoObjects)
        {
        }

    }
}
