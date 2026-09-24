using Sango.Core.Tools;
using TKNewtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Sango.Core.Action
{
    /// <summary>
    /// 势力建筑动作基类
    /// </summary>
    public abstract class ForceBuildingActionBase : ForceActionBase
    {
        /// <summary>
        /// 数值
        /// </summary>
        protected int value;
        /// <summary>
        /// 建筑类型集合
        /// </summary>
        protected List<int> kinds;

        /// <summary>
        /// 初始化动作
        /// </summary>
        /// <param name="p">参数</param>
        /// <param name="sangoObjects">游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            value = p.Value<int>("value");
            JArray kindsArray = p.Value<JArray>("kinds");
            if (kindsArray != null)
            {
                kinds = new List<int>(kindsArray.Count);
                for (int i = 0; i < kindsArray.Count; i++)
                    kinds.Add(kindsArray[i].ToObject<int>());
            }
        }

        /// <summary>
        /// 检查建筑是否符合条件
        /// </summary>
        /// <param name="buildingBase">建筑对象</param>
        /// <returns>是否符合条件</returns>
        public virtual bool CheckForceBuilding(BuildingBase buildingBase)
        {
            if (Force != buildingBase.BelongForce) return false;
            if (kinds != null && !kinds.Contains(buildingBase.BuildingType.kind)) return false;
            return true;
        }

    }
}
