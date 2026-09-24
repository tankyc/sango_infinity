using TKNewtonsoft.Json;

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
            if (atk.ObjectType != SangoObjectType.Troops)
                return;
            Troop troop = (Troop)atk;
            BelongCity?.OnBuildingDestroy(this);
            BelongCity = troop.BelongCity;
            BelongCorps = troop.BelongCorps;
            BelongCity?.OnBuildingCreate(this);
            durability = DurabilityLimit / 2;
            Render?.UpdateRender();
        }
        
    }
}
