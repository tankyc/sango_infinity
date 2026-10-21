using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 特技 #53 护卫：本部队的武将不会负伤（三国志11 口径："同部队的武将不会战死、负伤"）。
    ///
    /// 【拦截点为什么放在受伤接口上】
    ///   所有"让武将负伤"的效果最终都收敛到 <see cref="PersonInjury.Injure"/>，
    ///   而本动作订阅的是它在**写入伤病之前**派发的询问事件
    ///   <see cref="GameEvent.OnPersonCheckInjury"/>，把 canInjure 置 false 即免疫。
    ///   于是：
    ///     · 特技 #52 猛者 之类的伤害来源不需要知道护卫的存在（也不用各自加判断）；
    ///     · 两个特技的订阅先后无所谓 —— 无论谁先订阅，判定都发生在写伤病之前，
    ///       不会出现"先打伤、护卫再回滚"的顺序坑；
    ///     · 以后新增负伤来源（战法、陷阱、剧情）自动受护卫约束。
    ///
    /// 保护范围是"同部队"：护卫武将自己在内（他也在本部队编制里），用
    /// <see cref="Troop.HasPerson"/> 判编制成员，避免 person.mBelongTroop 在收尾/俘虏流程里被改写导致误判。
    /// 接入事件：OnPersonCheckInjury
    /// </summary>
    public class TroopPreventPersonInjury : TroopActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnPersonCheckInjury += OnPersonCheckInjury;
        }

        public override void Clear()
        {
            GameEvent.OnPersonCheckInjury -= OnPersonCheckInjury;
        }

        /// <summary>
        /// 只拦截"本部队武将"的本次负伤，其它部队（包括敌对部队）不受影响。
        /// </summary>
        /// <param name="person">待负伤的武将</param>
        /// <param name="source">伤害来源（部队 / 战法 / 单挑等，可空）</param>
        /// <param name="canInjure">是否允许负伤，置 false 即免疫</param>
        private void OnPersonCheckInjury(Person person, SangoObject source, OverrideData<bool> canInjure)
        {
            if (Troop == null || person == null) return;
            if (!Troop.HasPerson(person)) return;

            canInjure.Value = false;
        }
    }
}
