using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 一次性消耗品：授予后延长武将寿命并让道具本身消失（万寿果：武将死亡年 +10）。
    ///
    /// <para>
    /// 【装配方式】配在 <c>ItemType.actionEntities</c> 里，由
    /// <c>Person.InitPersonActions</c> → <c>EquippedWeapon?.InitActions(...)</c> 一类调用装配，
    /// 宿主是 <see cref="Person"/>。武将身上没有这件道具时，本 Action 不会被创建，
    /// 因此"授予后移除道具"会让它在下一次重建时自然消失，不需要额外写反装配逻辑。
    /// </para>
    /// <para>
    /// 【生效时机】<see cref="GameEvent.OnItemGranted"/>，由 <c>ItemGrantRules.Grant</c>
    /// 在 `InitPersonActions()` 之后派发 —— 那时本 Action 刚装配好，正好接得住。
    /// </para>
    /// <para>
    /// 【配置要求】万寿果的 itemType 必须是不占装备槽的类型（如珍品），
    /// 否则 <c>EquipToSlot</c> 会把它装到装备栏，`itemStore.Remove` 移除不到那一件。
    /// </para>
    /// </summary>
    public class PersonAddLifeYear : PersonActionBase
    {
        /// <summary>延长的年数</summary>
        int years;

        /// <summary>监听的物品 Id（可由参数显式指定，缺省取装配宿主道具 Id）</summary>
        int itemId;

        /// <summary>
        /// 初始化。
        /// <para>JSON 参数：years（延长年数）、itemId（可选，缺省用装配宿主的道具 Id）。</para>
        /// </summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">装配宿主（[0] 通常是 Person 本人）</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            ItemType hostItem = null;
            if (sangoObjects != null)
            {
                for (int i = 0; i < sangoObjects.Length; i++)
                {
                    hostItem = sangoObjects[i] as ItemType;
                    if (hostItem != null)
                        break;
                }
            }

            years = p.Value<int>("years");
            itemId = p.Value<int?>("itemId") ?? (hostItem != null ? hostItem.Id : 0);

            if (years <= 0 || itemId <= 0)
            {
                Sango.Log.Warning($"PersonAddLifeYear 参数不完整（years={years}, itemId={itemId}），不会生效");
                return;
            }

            GameEvent.OnItemGranted += OnItemGranted;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnItemGranted -= OnItemGranted;
        }

        /// <summary>
        /// 道具授予后：延长寿命并从背包移除该道具。
        /// </summary>
        /// <param name="city">执行授予的城市</param>
        /// <param name="person">目标武将</param>
        /// <param name="item">被授予的道具</param>
        void OnItemGranted(City city, Person person, ItemType item)
        {
            if (item == null || person == null)
                return;
            if (item.Id != itemId)
                return;

            // 只对"装配本 Action 的那个武将"生效：
            // 同城其他武将也被赐予万寿果时，不该把寿命加到这个人身上。
            if (Person != null && person != Person)
                return;

            // 【重要】yearDead <= 0 表示"不限寿命"（自建武将常见），
            // 此时 +10 会把它变成"只能再活 10 年"，必须跳过。
            if (person.yearDead > 0)
            {
                person.yearDead += years;
                Sango.Log.Info($"万寿果：{person.Name} 寿命延长 {years} 年（寿终年 {person.yearDead}）");
            }
            else
            {
                Sango.Log.Info($"万寿果：{person.Name} 为不限寿命，本次不产生效果");
            }

            // 一次性消耗：授予后从背包移除，实现"赐予后消失"
            person.itemStore.Remove(item, 1);
        }
    }
}
