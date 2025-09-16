using LuaInterface;
using Sango.Game.Render;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Sango.Game
{
    /// <summary>
    /// 部队
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public partial class Military : SangoObject
    {
        public Military()
        {
            IsAlive = true;
        }

        /// <summary>
        /// 所属势力
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Force>))]
        [JsonProperty]
        public Force BelongForce;

        /// <summary>
        /// 所属势力
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Corps>))]
        [JsonProperty]
        public Corps BelongCorps;

        /// <summary>
        /// 所属城池
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<City>))]
        [JsonProperty]
        public City BelongCity;


        /// <summary>
        /// 统领
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<Person>))]
        [JsonProperty]
        public Person Leader;

        /// <summary>
        /// 部队类型
        /// </summary>
        [JsonConverter(typeof(Id2ObjConverter<TroopType>))]
        [JsonProperty]
        public TroopType TroopType;

        /// <summary>
        /// 士气
        /// </summary>
        [JsonProperty] public int morale;
        /// <summary>
        /// 战意
        /// </summary>
        [JsonProperty] public int energy;
        /// <summary>
        /// 数量
        /// </summary>
        [JsonProperty] public int troops;
        /// <summary>
        /// 伤兵数量
        /// </summary>
        [JsonProperty] public int woundedTroops;

       

        public int MoveAbility { get { return TroopType.move; } set { } }

        //public bool IsFull { get { return troops >= TroopType.limitNum; } }
        //public int LimitNum { get { return TroopType.limitNum; } }
        //public int FightPower
        //{
        //    get
        //    {
        //        return troops / TroopType.limitNum * TroopType.fightPower;
        //    }
        //}

        public override void OnScenarioPrepare(Scenario scenario)
        {
        }

        /// <summary>
        /// 根据武将返回一个0-100的匹配度
        /// </summary>
        /// <param name="leader"></param>
        /// <returns></returns>
        public int GetMatchingPercent(Person leader)
        {
            switch (TroopType.influenceAbility)
            {
                case (int)AbilityType.Halberd:
                    return leader.HalberdLv * 100 + leader.Command * 130 / 100 + leader.Strength * 120 / 100;
                case (int)AbilityType.Crossbow:
                    return leader.CrossbowLv * 100 + leader.Intelligence * 110 / 100 + leader.Command * 110 / 100 + leader.Strength * 130 / 100;
                case (int)AbilityType.Horse:
                    return leader.HorseLv * 100 + leader.Command * 120 / 100 + leader.Strength * 130 / 100;
                case (int)AbilityType.Machine:
                    return leader.MachineLv * 100;
                case (int)AbilityType.Water:
                    return leader.WaterLv * 100;
                default:
                    return 0;
            }
        }

    }
}
