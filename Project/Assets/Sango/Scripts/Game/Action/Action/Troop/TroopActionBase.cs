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

        #region 伤害修正抑制（陷阱等"已自行完成特技修正"的伤害）

        /// <summary>
        /// "本次兵力变化已由调用方自行完成特技修正"的抑制计数。
        ///
        /// 【为什么需要】原版把伤害拆成三条互不叠加的函数：
        ///   · 战斗伤害 <c>calc_attack_damage</c>（s11_sys_damage.cpp:278）
        ///   · 火焰伤害 <c>calc_fire_damage</c>（:991）
        ///   · 陷阱伤害 <c>calc_trap_damage</c>（:1096）
        /// 「藤甲」在三条里各自处理（战斗 /2、火焰 *2、陷阱落石 /2 火系 *2），互不叠加。
        ///
        /// 本项目把这些特技做成挂在 <c>GameEvent.OnTroopChangeTroops</c> 上的 Action
        /// （TroopChangeDamage / TroopSetDamage），而陷阱结算（<see cref="BuildingTrapEffect"/>）
        /// 已经在 CalcTrapDamage 里按 calc_trap_damage 算过藤甲 / 火神 / 踏破 / 爆药，
        /// 若再走一遍 Action 就是二次修正：火系 ×2 又被 -50% 抵回 ×1，落石 /2 再 /2。
        ///
        /// 用计数而不是 bool：结算过程可能嵌套（伤害致死 → 死亡处理里再次改兵力），
        /// 只有最外层 finally 归零才真正恢复。
        /// </summary>
        static int suppressDamageModifierCount;

        /// <summary>当前是否处于"伤害修正 Action 抑制"区间</summary>
        public static bool IsDamageModifierSuppressed => suppressDamageModifierCount > 0;

        /// <summary>
        /// 进入抑制区间：区间内 <c>GameEvent.OnTroopChangeTroops</c> 不再被
        /// <see cref="TroopChangeDamage"/> / <see cref="TroopSetDamage"/> 修正。
        /// </summary>
        public static void BeginSuppressDamageModifier()
        {
            suppressDamageModifierCount++;
        }

        /// <summary>
        /// 退出抑制区间，必须与 <see cref="BeginSuppressDamageModifier"/> 成对调用（建议 try/finally）。
        /// </summary>
        public static void EndSuppressDamageModifier()
        {
            if (suppressDamageModifierCount > 0)
                suppressDamageModifierCount--;
        }

        #endregion

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
        /// <param name="skill">发起本次伤害的战法；非战法伤害（火焰灼烧等）为 null</param>
        /// <param name="isNormalSkill"></param>
        /// <returns></returns>
        public bool CheckIsNormalSkill(SkillInstance skill, int isNormalSkill)
        {
            // 【空引用修复】skill 为 null 表示这次兵力变化不是战法打出来的。
            // 火焰灼烧的调用链是 FireDamageEvent.Enter → Troop.ChangeTroops(-damage, fire, 0)
            // → GameEvent.OnTroopChangeTroops → TroopSetDamage.CheckTroop，atker 是 Fire，
            // `atker as SkillInstance` 得到 null；而 isDefender != 0（受击方）的分支不校验 skill，
            // 于是直接走到这里，原写法 skill.IsNormal() 抛 NullReferenceException，
            // 整个 FireDamageEvent 被 RenderEventQueue 丢弃，火焰伤害不生效。
            //
            // 语义口径：isNormal 是"战法属性"过滤，没有战法就没有这个属性，
            // 因此只在 isNormalSkill 明确要求（1 一般攻击 / 2 非一般攻击）时视为不匹配，
            // 0 / -1（不限）照常放行。
            //
            // 这里不能对 null 一刀切拒绝：建筑反击（BuildingAttackEvent.cs:77）这类
            // "非战法但属于战斗伤害"的结算仍要受 isDefender = 1 的特技减伤影响，
            // 它的 atker 同样不是 SkillInstance。
            // 火焰灼烧在 TroopTroopActionBase.CheckTroop 里按 atker is Fire 前置拦掉；
            // 陷阱引爆 / 落石在 BuildingTrapEffect 里用抑制区间拦掉（它们的藤甲倍率
            // 已在 CalcTrapDamage 内算完），两者都不依赖这里的 null 分支。
            if (skill == null)
                return isNormalSkill != 1 && isNormalSkill != 2;

            if ((isNormalSkill == 1 && !skill.IsNormal()) || (isNormalSkill == 2 && skill.IsNormal()))
                return false;
            return true;
        }

        /// <summary>
        /// 是否是远程 1远程 2近战 0都可以 
        /// </summary>
        /// <param name="skill">发起本次伤害的战法；非战法伤害（火焰灼烧等）为 null</param>
        /// <param name="isRangeSkill"></param>
        /// <returns></returns>
        public bool CheckIsRangeSkill(SkillInstance skill, int isRangeSkill)
        {
            // 【空引用修复】同上：非战法伤害没有射程属性，只在明确要求射程时视为不匹配，理由见 CheckIsNormalSkill。
            if (skill == null)
                return isRangeSkill != 1 && isRangeSkill != 2;

            if ((isRangeSkill == 1 && !skill.IsRange()) || (isRangeSkill == 2 && skill.IsRange()))
                return false;
            return true;
        }

        public override void Execute(Trigger trigger)
        {
        }

    }
}
