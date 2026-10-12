/*
 * 文件名：CityTroopFactory.cs
 * 描述：城池 AI 出兵的公共收尾逻辑。
 *       把「登记部队 → 设为当前活动部队 → 刷新渲染」这一在 CityAI 中重复出现
 *       8 次以上的样板集中到一处，避免遗漏与重复维护。
 */

namespace Sango.Core
{
    /// <summary>
    /// 城池 AI 出兵工厂。
    /// </summary>
    public static class CityTroopFactory
    {
        /// <summary>
        /// 出征收尾：把已组建的部队登记进场景、标记为城市当前活动部队并刷新渲染。
        ///
        /// 同时按任务确定部队角色（<see cref="TroopRole"/>），使同一任务下的部队
        /// 因角色不同而采取不同的作战风格（攻坚 / 防守 / 骚扰 / 游击）。
        /// </summary>
        /// <param name="city">出兵城市</param>
        /// <param name="troop">已组建的部队</param>
        /// <param name="scenario">场景对象</param>
        /// <returns>登记后的部队（troop 为 null 时返回 null）</returns>
        public static Troop EmitTroop(City city, Troop troop, Scenario scenario)
        {
            if (troop == null || city == null)
                return null;

            troop = city.EnsureTroop(troop, scenario);

            // 【前置判断】出征前先确认部队能否走出城头：城池被完全围困
            // （移动范围内没有可停留的空地）时，部队一出征就会卡在城头无法行动。
            // 此时取消本次出征，退回组建成本并返回 null，让调用方跳过后续日志 / 任务。
            if (!TroopAIUtility.CanLeaveBuildingCell(troop, scenario))
            {
                Sango.Log.Warning($"势力[{city.BelongForce?.Name}]在{city.Name}出兵失败:城池被完全围困,部队无法出城,已取消本次出征");
                CancelEmitTroop(city, troop);
                return null;
            }

            city.CurActiveTroop = troop;
            city.Render?.UpdateRender();

            // 【分级策略】出征时固化部队角色：Auto 时依据任务与属性自动推导一次，
            // 避免每回合重算导致角色漂移、行为抖动。
            if (troop.role == TroopRole.Auto)
                troop.role = troop.ResolveRole();

            return troop;
        }

        /// <summary>
        /// 出征前置判断失败时回退：先收集成员，再进城返还兵力 / 钱粮 / 兵装，
        /// 最后把成员放回城市的空闲名单（与 CityExpedition.OnBack 的取消约定一致）。
        /// </summary>
        /// <param name="city">出兵城市</param>
        /// <param name="troop">已登记但需取消的部队</param>
        static void CancelEmitTroop(City city, Troop troop)
        {
            // 必须在进城解散之前收集成员：EnterCity 会 Clear 部队，之后无法再遍历成员
            System.Collections.Generic.List<Person> members = new System.Collections.Generic.List<Person>();
            troop.ForEachPerson(person => members.Add(person));

            troop.EnterCity(city);

            foreach (Person person in members)
            {
                // 幂等：部分调用方（如补给队）在 EmitTroop 之后才从 freePersons 摘人，
                // 取消时该成员可能仍在名单里，重复 Add 会产生重复引用。
                if (!city.freePersons.Contains(person))
                    city.freePersons.Add(person);
                person.ActionOver = false;
            }
        }
    }
}
