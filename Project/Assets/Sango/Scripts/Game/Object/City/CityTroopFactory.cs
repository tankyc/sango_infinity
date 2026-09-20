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
            city.CurActiveTroop = troop;
            city.Render?.UpdateRender();

            // 【分级策略】出征时固化部队角色：Auto 时依据任务与属性自动推导一次，
            // 避免每回合重算导致角色漂移、行为抖动。
            if (troop.role == TroopRole.Auto)
                troop.role = troop.ResolveRole();

            return troop;
        }
    }
}
