using Newtonsoft.Json.Linq;
using Sango.Core.Tools;

namespace Sango.Core
{
    /// <summary>
    /// 所在格是否在燃烧（条件）。
    ///
    /// <para>
    /// 【用途】支撑"趁火打劫"类设计（Excel 55462 趁炎：攻击所在格着火的敌部队时物理伤害 +20%）。
    /// Excel 复核结论明确要求"注册 CellHasFire 条件并应用于敌方物理攻击"——条件类此前不存在，
    /// 只能靠 <c>TargetFire</c> 之类的上下文对象名去猜，配不出来。
    /// </para>
    /// <para>
    /// 【判定口径】<c>Cell.fire != null</c>：火焰对象在 <c>Fire.Clear()</c> 里会把
    /// <c>cell.fire</c> 置 null（<c>Fire.cs:292</c>），所以非空即"该格正在燃烧"，
    /// 不需要再判 <c>IsAlive</c>。
    /// </para>
    /// <para>
    /// 【方向】<c>compareTarget</c>：<c>target</c>（缺省，检查目标所在格）/
    /// <c>self</c>（检查施法方所在格）。55462 用缺省值即可。
    /// </para>
    /// JSON 参数：<c>compareTarget</c>（target / self）。
    /// </summary>
    public class CellHasFire : Condition
    {
        /// <summary>true = 检查目标所在格；false = 检查施法方所在格</summary>
        bool compareTarget = true;

        /// <summary>初始化条件</summary>
        /// <param name="p">JSON 参数对象</param>
        /// <param name="sangoObjects">相关游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            compareTarget = (p.Value<string>("compareTarget") ?? "target") != "self";
        }

        /// <summary>
        /// 检查对应一方所在格是否在燃烧。
        ///
        /// <para>
        /// 【为什么优先取 Cell 而不是 Troop】<c>IConditionDatabase</c> 里没有 Fire 槽位
        /// （<c>TroopActionConditionDatabase</c> 虽然额外声明了 ActiveFire / TargetFire，
        /// 但那两个属性不在接口上，条件类按接口拿不到）。
        /// 只能用 <c>TargetCell / ActionCell</c>；只有格子槽为空时才回退到部队的 cell，
        /// 这样"目标格着火但目标不是部队"（建筑、空地落点）也能判出来。
        /// </para>
        /// </summary>
        /// <param name="database">条件上下文</param>
        /// <returns>着火返回 true；格子与部队都拿不到时返回 false</returns>
        public override bool Check(IConditionDatabase database)
        {
            if (database == null)
                return false;

            Cell cell = compareTarget ? database.TargetCell : database.ActionCell;
            if (cell == null)
            {
                Troop troop = compareTarget ? database.TargetTroop : database.ActionTroop;
                cell = troop != null ? troop.cell : null;
            }

            return cell != null && cell.fire != null;
        }
    }
}
