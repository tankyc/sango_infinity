using Newtonsoft.Json;

namespace Sango.Core
{
    /// <summary>
    /// 战略建筑,存在于地图中,可被占领,用于提升所属城市数据
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class StrategyBuilding : Building
    {
        /// <summary>
        /// 建筑被摧毁时的回调方法
        /// 战略建筑无法被摧毁,耐久降为0后会改变归属至攻击者势力
        /// </summary>
        /// <param name="atk">攻击者</param>
        public override void OnFall(SangoObject atk)
        {
            // 【统一解析】战法破设施传进来的是 SkillInstance（施法部队在 master 上），
            // 原先只认 `ObjectType == Troops` 会直接 return，战略建筑因此永远不换归属；
            // 同时 atk 可能为 null（BanditCampPlacer 直接传 null，测试也会），先兜住空引用。
            Troop troop = atk?.AsTroop();
            if (troop == null || troop.BelongCity == null)
                return;

            BelongCity?.OnBuildingDestroy(this);
            BelongCity = troop.BelongCity;
            BelongCorps = troop.BelongCorps;
            BelongCity?.OnBuildingCreate(this);
            durability = DurabilityLimit / 2;
            Render?.UpdateRender();
        }
        
    }
}
