using Newtonsoft.Json.Linq;
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
            public Person ActionPerson => self?.master.Leader;
            public Person TargetPerson => atk_cell.troop?.Leader;
            public Troop ActionTroop => self?.master;
            public Troop TargetTroop => atk_cell.troop;
            public Cell ActionCell => self?.master.cell;
            public Cell TargetCell => atk_cell;
            public City ActionCity => self?.master.BelongCity;
            public City TargetCity => atk_cell.troop?.BelongCity ?? atk_cell.building?.BelongCity;
            public Corps ActionCorps => self?.master.BelongCorps;
            public Corps TargetCorps => atk_cell.troop?.BelongCorps ?? atk_cell.building?.BelongCorps;
            public Force ActionForce => self?.master.BelongForce;
            public Force TargetForce => atk_cell.troop?.BelongForce ?? atk_cell.building?.BelongForce;

            public Fire ActiveFire => self?.master.cell.fire;
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
            public Person ActionPerson => self?.master.Leader;
            public Person TargetPerson => null;
            public Troop ActionTroop => self?.master;
            public Troop TargetTroop => null;
            public Cell ActionCell => self?.master.cell;
            public Cell TargetCell => null;
            public City ActionCity => self?.master.BelongCity;
            public City TargetCity => null;
            public Corps ActionCorps => self?.master.BelongCorps;
            public Corps TargetCorps => null;
            public Force ActionForce => self?.master.BelongForce;
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
            public City ActionCity => self.BelongCity;
            public City TargetCity => null;
            public Corps ActionCorps => self.BelongCorps;
            public Corps TargetCorps => null;
            public Force ActionForce => self.BelongForce;
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
            // 防御性判断：装配方传空数组时不应抛 IndexOutOfRangeException
            if (sangoObjects == null || sangoObjects.Length == 0)
                return;

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
        /// 检查部队当前所处地形是否与指定兵种类型匹配。
        ///
        /// checkTroopTypeKind：0 全兵种全地形 / -1 仅陆地 / -2 仅水上 / >0 指定兵种 Kind。
        /// 说明：当前没有调用方（由各 Action 自己内联判断），保留作为公共工具。
        /// </summary>
        /// <param name="troop">部队对象</param>
        /// <param name="checkTroopTypeKind">兵种类型过滤值</param>
        /// <returns>是否匹配</returns>
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

        public override void Execute(Trigger trigger)
        {
        }

    }
}
