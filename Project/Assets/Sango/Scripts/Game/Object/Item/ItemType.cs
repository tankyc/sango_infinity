using TKNewtonsoft.Json;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]

    public class ItemType : SangoObjectExtensionData
    {
        /// <summary>
        /// 主类型
        /// </summary>
        [JsonProperty] public byte kind;

        /// <summary>
        /// 次类型
        /// </summary>
        [JsonProperty] public byte storeKind;

        /// <summary>
        /// 下一阶道具
        /// </summary>
        [JsonProperty] public int nextId;

        /// <summary>
        /// 描述
        /// </summary>
        [JsonProperty] public string desc;

        /// <summary>
        /// 图标
        /// </summary>
        [JsonProperty] public string icon;

        /// <summary>
        /// 是否可存储
        /// </summary>
        [JsonProperty] public bool store;

        /// <summary>
        /// 额外费用
        /// </summary>
        [JsonProperty] public int cost;

        /// <summary>
        /// 产出建筑
        /// </summary>
        [JsonProperty] public int createBuildingKind;

        /// <summary>
        /// 所需科技
        /// </summary>
        [JsonProperty] public int validTechId;

        /// <summary>
        /// 额外参数1,
        /// </summary>
        [JsonProperty] public int p1;

        /// <summary>
        /// 额外参数2,
        /// </summary>
        [JsonProperty] public int p2;

        /// <summary>
        /// 额外参数3,
        /// </summary>
        [JsonProperty] public int p3;


        public bool IsValid(Force force)
        {
            if (validTechId > 0)
                return force.HasTechnique(validTechId);

            // 检查科技
            return true;
        }
        public bool IsWeapon()
        {
            return kind == (int)ItemKindType.Weapon;
        }

        public bool IsBoat()
        {
            return kind == (int)ItemKindType.Boat;
        }

        public bool IsMachine()
        {
            return kind == (int)ItemKindType.Machine;
        }

        public bool IsHorse()
        {
            return kind == (int)ItemKindType.Horse;
        }

    }
}
