using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 按**百分比**缩放火焰伤害（乘法），在已有 <see cref="Fire.damage"/> 基础上乘算。
    ///
    /// 接入事件：OnFireAdd（火焰生成时）。执行时机在 <c>SetFire.Action</c> 派发
    /// <c>GameEvent.OnFireAdd</c> 之后、<c>fire.Action()</c>（对格上部队/建筑结算）之前，
    /// 因此这里改动的 damage 会立刻被本次燃烧使用。
    ///
    /// 【单位为什么是百分比而不是万分比】
    ///   · 同为"覆盖"语义的 <see cref="TroopSetFireDamage"/> 用万分比（10000 = 1 倍），
    ///     因为它要对剧本参数 <c>baseFireDamage</c> 做绝对值改写；
    ///   · 本 Action 是"在现值上做倍率"，数据侧按百分比书写更直观：
    ///     藤甲（Features.json Id 32）配 <c>value = 200</c> 即"火伤 ×2"，
    ///     与同一条特技里 <c>TroopChangeDamage</c> 的 <c>value = -50</c>（百分比）同口径。
    ///     早前误按万分比实现，会把 200 算成 ×0.02，等于让藤甲几乎免疫火焰。
    ///
    /// 原版出处：s11_sys_damage.cpp:15-16 —— 藤甲（SkillId_Toukou）的着火移动伤害
    /// <c>damage->troops_damage *= 2</c>，即"火以外的损伤减半、火伤翻倍"。
    /// </summary>
    public class TroopModifyFireDamage : TroopActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnFireAdd += OnFireAdd;
        }

        public override void Clear()
        {
            GameEvent.OnFireAdd -= OnFireAdd;
        }

        void OnFireAdd(SkillInstance skillInstance, Fire fire)
        {
            if (fire == null) return;
            if (skillInstance == null || skillInstance.master == null) return;

            if (Force != null && skillInstance.master.BelongForce != Force) return;
            if (Troop != null && Troop != skillInstance.master) return;

            // 百分比倍率：value = 100 保持原值，200 翻倍，50 减半
            fire.damage = fire.damage * value / 100;
        }
    }
}
