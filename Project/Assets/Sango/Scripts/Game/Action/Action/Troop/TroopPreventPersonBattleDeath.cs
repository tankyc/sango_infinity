using Sango.Core.Tools;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 特技 #53 护卫：本部队的武将不会战死（三国志11 口径："同部队的武将不会战死、负伤"）。
    ///
    /// 【拦截点为什么放在战死接口上】
    ///   战场死亡统一走 <see cref="Person.DeadInBattle"/>（单挑落败身死等），
    ///   本动作订阅的是它在**任何死亡处理之前**派发的询问事件
    ///   <see cref="GameEvent.OnPersonCheckBattleDeath"/>，把 canDie 置 false 即免死。
    ///   于是和"免伤"一样：判定先于结算、与订阅先后无关、也不会出现"已经死了再复活"的回滚。
    ///
    /// 【为什么不会误免处决 / 剧情杀】
    ///   处决（斩首）、病逝、剧本事件死亡走的是 <see cref="Person.Dead"/>，不经过本事件，
    ///   所以被擒的同部队武将照样会被处斩 —— 护卫只挡战场上的战死。
    ///
    /// 保护范围是"同部队"：护卫武将自己在内（他也在本部队编制里），用
    /// <see cref="Troop.HasPerson"/> 判编制成员，避免 person.mBelongTroop 在收尾/俘虏流程里被改写导致误判。
    /// </summary>
    public class TroopPreventPersonBattleDeath : TroopActionBase
    {
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);
            GameEvent.OnPersonCheckBattleDeath += OnPersonCheckBattleDeath;
        }

        public override void Clear()
        {
            GameEvent.OnPersonCheckBattleDeath -= OnPersonCheckBattleDeath;
        }

        /// <summary>
        /// 只拦截"本部队武将"的本次战死，其它部队（包括敌对部队）不受影响。
        /// </summary>
        /// <param name="person">待战死的武将</param>
        /// <param name="source">致死来源（部队 / 决斗对手等，可空）</param>
        /// <param name="canDie">是否允许战死，置 false 即免死</param>
        private void OnPersonCheckBattleDeath(Person person, SangoObject source, OverrideData<bool> canDie)
        {
            if (Troop == null || person == null) return;
            if (!Troop.HasPerson(person)) return;

            canDie.Value = false;
        }
    }
}
