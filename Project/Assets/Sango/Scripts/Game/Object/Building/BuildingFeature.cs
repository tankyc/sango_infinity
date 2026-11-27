using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sango.Core.Action;

namespace Sango.Core
{
    /// <summary>
    /// 建筑特色类型（数据字段 type；只用于分类 / UI 图标选择，具体行为由 actionEntities 决定）。
    /// </summary>
    public enum BuildingFeatureType
    {
        /// <summary>额外资源收入（金 / 粮 / 技巧点，每 N 回合）</summary>
        ResourceIncome = 0,
        /// <summary>额外道具收入（每 N 回合）</summary>
        ItemIncome = 1,
        /// <summary>解锁可建造建筑</summary>
        UnlockBuilding = 2,
        /// <summary>光环效果（通常配置 BuildingImproveTroop* 类 Action）</summary>
        Aura = 3,
    }

    /// <summary>
    /// 建筑特色条目。
    ///
    /// 数据文件：Data/Common/BuildingFeatures.json
    /// 建筑通过 BuildingType.featureIds（int[]）直接引用一个或多个特色条目；
    /// 建筑初始化时把这些条目的 Action 依次追加到 Building.actionList。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class BuildingFeature : SangoObject
    {
        /// <summary>特色类型（<see cref="BuildingFeatureType"/>）</summary>
        [JsonProperty] public int type;

        /// <summary>图标资源名 / 路径（供界面显示，空则用默认图标）</summary>
        [JsonProperty] public string icon;

        /// <summary>
        /// 特色 Action 列表；建筑初始化时按顺序追加到 Building.actionList。
        /// 例：{"class":"BuildingFeatureResourceIncome","resourceType":0,"value":300,"intervalTurns":3}
        /// </summary>
        [JsonProperty] public JArray actionEntities;

        /// <summary>
        /// 该特色解锁的可建造建筑类型 Id 列表。
        /// 与目标 BuildingType.needFeatureIds 配合使用：目标建筑要求城市里存在对应特色。
        /// </summary>
        [JsonProperty] public int[] unlockBuildingTypeIds;

        /// <summary>
        /// 本特色是否解锁指定建筑类型。
        /// </summary>
        /// <param name="buildingTypeId">目标建筑类型 Id</param>
        /// <returns>解锁返回 true</returns>
        public bool UnlocksBuilding(int buildingTypeId)
        {
            if (buildingTypeId <= 0 || unlockBuildingTypeIds == null)
                return false;
            for (int i = 0; i < unlockBuildingTypeIds.Length; i++)
            {
                if (unlockBuildingTypeIds[i] == buildingTypeId)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 把本特色的 Action 实例化并追加到建筑动作列表。
        /// </summary>
        /// <param name="list">建筑动作列表</param>
        /// <param name="building">宿主建筑</param>
        public void InitActions(List<ActionBase> list, Building building)
        {
            if (list == null || actionEntities == null)
                return;

            for (int i = 0; i < actionEntities.Count; i++)
            {
                JObject node = actionEntities[i] as JObject;
                if (node == null)
                    continue;

                ActionBase action = ActionBase.Create(node.Value<string>("class"));
                if (action == null)
                {
                    Sango.Log.Warning($"*[建筑特色]{Name} 的 Action 类未注册：{node.Value<string>("class")}");
                    continue;
                }
                action.Init(node, building);
                list.Add(action);
            }
        }
    }
}
