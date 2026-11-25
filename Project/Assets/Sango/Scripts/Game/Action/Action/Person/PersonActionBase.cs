using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 武将个人 Action 基类。
    ///
    /// <para>
    /// 【为什么需要它】项目原有的宿主基类只有 Troop / City / Building / Force / Aura 五类。
    /// 这些基类都从 <c>sangoObjects</c> 里取"部队 / 城池 / 建筑"，取不到就判定为"未生效"。
    /// 但"伯乐（登用成功率）""蜀相（人事与开发）"这类特技的作用对象是**武将本人**，
    /// 由 <c>Person.InitPersonActions</c> 装配（<c>Person.cs:4937</c>，kind 为 PersonRelationship），
    /// 宿主是 Person 而不是任何部队。没有这个基类，这类特技只能被迫塞进 TroopActionBase，
    /// 而武将在家时没有部队，特技就永远不生效。
    /// </para>
    /// <para>
    /// 【宿主解析】按 <c>sangoObjects</c> 顺序取第一个 Person 作为本人、第一个 Force 作为所属势力。
    /// 两者都可为空，子类必须自己判空后决定是否订阅事件。
    /// </para>
    /// </summary>
    public abstract class PersonActionBase : ActionBase
    {
        /// <summary>特技持有者本人（由 Person.InitPersonActions 传入）</summary>
        protected Person Person;

        /// <summary>所属势力（若装配时一并传入）</summary>
        protected Force Force;

        /// <summary>
        /// 初始化：从装配参数里解析宿主。
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">装配宿主（[0] 通常是 Person 本人）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            if (sangoObjects == null)
                return;

            for (int i = 0; i < sangoObjects.Length; i++)
            {
                SangoObject obj = sangoObjects[i];
                if (obj == null)
                    continue;
                if (Person == null)
                    Person = obj as Sango.Core.Person;
                if (Force == null)
                    Force = obj as Sango.Core.Force;
            }
        }

        /// <summary>
        /// 武将个人类 Action 都是事件驱动的被动效果，不需要外部主动触发。
        /// </summary>
        /// <param name="trigger">触发参数（未使用）</param>
        public override void Execute(Trigger trigger)
        {
        }
    }
}
