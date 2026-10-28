using Newtonsoft.Json.Linq;

namespace Sango.Core
{
    /// <summary>
    /// 足止（Ashidome）—— 行动力归零，无法移动，但可原地攻击 / 施法。
    ///
    /// 【为什么是空效果类】本状态的全部行为都通过 <see cref="Buff.troopMoveCoef"/> = 0 表达：
    /// <c>Troop.GetTargetMoveAbility</c> 末尾会乘这个系数，归零后任何寻路都走不动。
    /// 这里保留一个效果类只是为了让 Buff 在表现层能正常挂载 / 显示 / 解除。
    ///
    /// 【原版说明】原版 UnitState_Ashidome 在 .cpp 里没有任何引用（死枚举），
    /// 所以效果区间是按"足止"字面语义定义的：能打不能走。
    /// </summary>
    public class MoveSeal : BuffEffect
    {
        public override void Init(JObject p, BuffInstance master)
        {
            base.Init(p, master);
        }

        public override void Clear()
        {
            base.Clear();
        }
    }
}
