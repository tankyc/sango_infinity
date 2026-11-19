using Sango.Core.Tools;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

namespace Sango.Core.Action
{
    /// <summary>
    /// 部队→部队类效果基类（"归属部队"与"被作用部队"分离的场景）。
    ///
    /// 公共参数：
    /// kinds      兵种类型过滤，null 表示不限制
    /// isDefender 0 攻击方 / 1 受击方
    /// isNormal   -1 不限 / 0 仅战法 / 1 仅一般攻击
    /// isRange    -1 不限 / 1 远程 / 2 近战
    /// isFire     0 不限 / 1 仅一般攻击（当前实现与 isNormal 重叠，数据中未使用）
    /// condition  额外条件（TroopSkillConditionDatabase）
    /// </summary>
    public abstract class TroopTroopActionBase : TroopActionBase
    {
        protected int checkLand;
        protected int isDefender;
        protected int isNormal;
        protected int isFire;
        protected int isRange;
        protected Condition condition;

        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            checkLand = p.Value<int>("checkLand");
            isDefender = p.Value<int>("isDefender");
            isNormal = p.Value<int>("isNormal");
            isRange = p.Value<int>("isRange");
            isFire = p.Value<int>("isFire");
            JObject conObj = p.Value<JObject>("condition");
            if (conObj != null)
            {
                // 【防御】class 名写错 / 未注册时 Create 返回 null，原写法会在下一行
                // null.Init 抛 NullReferenceException 并中断整个 Init ——
                // 后果是子类（如 TroopChangeSkillAttackRange）的 atkType / atkRange 读不到，
                // 保持默认 0，SetRange 就会生成非法的 [0, x] 命中偏移，
                // 最终在 Skill.GetAttackCells 的 Customize 分支越界崩溃。
                condition = Condition.Create(conObj.Value<string>("class"));
                if (condition != null)
                    condition.Init(conObj, sangoObjects);
                else
                    Sango.Log.Error($"部队 Action 的条件类未注册：{conObj.Value<string>("class")}，已忽略该条件");
            }
        }

        protected virtual bool CheckTroop(Troop defencer, SangoObject atker)
        {
            // 【陷阱】调用方已按原版 calc_trap_damage 自行完成特技修正（藤甲 / 火神 / 踏破 / 爆药），
            // 这里不能再套一层 TroopChangeDamage / TroopSetDamage，否则藤甲的火系 ×2 会被 -50% 抵回 ×1。
            if (IsDamageModifierSuppressed)
                return false;

            // 【类型严检】atker 可能是 Troop / SkillInstance（战法，部队挂在 master 上）/ Fire / null。
            // 这里只需要"是不是战法"这一个信息，用 as 取，取不到就当普通攻击处理。
            if (defencer == null)
                return false;

            Troop troop = defencer;
            SkillInstance skill = atker as SkillInstance;

            if (isDefender == 0)
            {
                if (skill == null) 
                    return false;

                // 施法部队可能已溃灭 / 已离场，master 为 null 时下面取 BelongForce 会空引用
                troop = skill.master;
                if (troop == null)
                    return false;
            }
            else if (atker is Fire)
            {
                // 【空引用修复】火焰灼烧不参与"部队伤害修正"类动作。
                //
                // 调用链：FireDamageEvent.Enter → Troop.ChangeTroops(-damage, fire, 0)
                //        → GameEvent.OnTroopChangeTroops → TroopSetDamage / TroopChangeDamage.CheckTroop
                // atker 是 Fire（不是 SkillInstance），isDefender != 0 的分支又不校验 skill，
                // 于是直接走到 CheckIsNormalSkill(skill = null, isNormal) 抛 NullReferenceException，
                // 整个 FireDamageEvent 被 RenderEventQueue 丢弃 —— 表现为"火焰伤害打不出来"，
                // 控制台刷屏 "[演出]FireDamageEvent 执行异常，已丢弃并继续"。
                //
                // 语义上也应当拒绝 —— 原版把"战斗伤害"和"火焰伤害"拆成了两个互不叠加的函数：
                //   · 战斗伤害 calc_attack_damage（s11_sys_damage.cpp:278），藤甲在 :527-531 做
                //     `damage.troops_damage /= 2`；
                //   · 火焰伤害 calc_fire_damage（s11_sys_damage.cpp:991），藤甲在 :1019-1034 做
                //     `damage.troops_damage *= 2`；
                //   · 火格移动伤害 get_movement_damage（:5），藤甲在 :15-16 做 `*= 2`。
                // 火焰伤害根本不进 calc_attack_damage，所以不会吃到那个 `/ 2`。
                // 本项目若让火焰也走 TroopChangeDamage（挂在 OnTroopChangeTroops 上，普攻/战法/火焰共用），
                // 「藤甲」（Features.json Id 32：TroopChangeDamage -50 + TroopModifyFireDamage 200）
                // 会算成 ×0.5×2 = ×1，火攻翻倍的效果正好被抵消。
                // 火焰伤害的倍率由 TroopModifyFireDamage（OnFireAdd，直接改 fire.damage）单独负责。
                //
                // 注意本分支只拦 Fire（火焰灼烧的 atker 就是 Fire 对象）。
                // 陷阱引爆 / 落石同样不该走 TroopChangeDamage，但它们在
                // BuildingTrapEffect.ApplyTroopDamage / ApplyRockfallDamage 里已经用
                // TroopActionBase.BeginSuppressDamageModifier 显式声明过，不在这里重复拦。
                // 建筑反击（BuildingAttackEvent.cs:77）属于战斗伤害，仍应受 isDefender = 1 的减伤影响。
                return false;
            }
            if (Force != null && troop.BelongForce != Force) return false;
            if (Troop != null && Troop != troop) return false;

            if (!CheckIsNormalSkill(skill, isNormal))
                return false;

            if (!CheckIsRangeSkill(skill, isRange))
                return false;

            // isFire：Skill 上并没有"是否火焰战法"的判定，这里的实现退化为
            // "isFire == 1 时只放行一般攻击"，与 isNormal == 1 完全重叠。
            // 现有配置未使用，建议留空；等 Skill 增加火焰标记后再实现真正的火焰过滤。
            if (isFire == 1 && skill != null)
                return false;

            if (checkLand == 1 && troop.IsInWater)
                return false;
            if (checkLand == 2 && !troop.IsInWater)
                return false;

            if (checkLand == 0 && kinds != null && !kinds.Contains(troop.LandTroopType.kind) && !kinds.Contains(troop.WaterTroopType.kind))
                return false;

            if (checkLand == 1 && kinds != null && !kinds.Contains(troop.LandTroopType.kind))
                return false;

            if (checkLand == 2 && kinds != null && !kinds.Contains(troop.WaterTroopType.kind))
                return false;

            if (condition != null && !condition.Check(new TroopActionConditionDatabase(skill, defencer.cell)))
                return false;

            return true;
        }
    }
}
