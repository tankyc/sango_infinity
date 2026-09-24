
using Sango.Render;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace Sango.Core
{
    /// <summary>
    /// 部队
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class Military : SangoObject
    {
        public Military()
        {
            IsAlive = true;
        }

        /// <summary>
        /// 所属势力的 id（存档数值）。注意：字段名一直叫 BelongForceId，但过去存的是对象，
        /// 这里改回"id + 对象访问器"，并把 JSON 键钉回最初的 BelongForce。
        /// </summary>
        [JsonProperty("BelongForce")]
        public int BelongForceId;

        Force mBelongForce;
        public Force BelongForce
        {
            get
            {
                if (mBelongForce == null && BelongForceId > 0) mBelongForce = IdRef.Resolve<Force>(BelongForceId);
                return mBelongForce;
            }
            set { mBelongForce = value; BelongForceId = value != null ? value.Id : 0; }
        }

        /// <summary>
        /// 所属军团的 id（存档数值）
        /// </summary>
        [JsonProperty("BelongCorps")]
        public int BelongCorpsId;

        Corps mBelongCorps;
        public Corps BelongCorps
        {
            get
            {
                if (mBelongCorps == null && BelongCorpsId > 0) mBelongCorps = IdRef.Resolve<Corps>(BelongCorpsId);
                return mBelongCorps;
            }
            set { mBelongCorps = value; BelongCorpsId = value != null ? value.Id : 0; }
        }

        /// <summary>
        /// 所属城池的 id（存档数值）
        /// </summary>
        [JsonProperty("BelongCity")]
        public int BelongCityId;

        City mBelongCity;
        public City BelongCity
        {
            get
            {
                if (mBelongCity == null && BelongCityId > 0) mBelongCity = IdRef.Resolve<City>(BelongCityId);
                return mBelongCity;
            }
            set { mBelongCity = value; BelongCityId = value != null ? value.Id : 0; }
        }


        /// <summary>
        /// 统领的 id（存档数值）
        /// </summary>
        [JsonProperty("Leader")]
        public int LeaderId;

        Person mLeader;
        public Person Leader
        {
            get
            {
                if (mLeader == null && LeaderId > 0) mLeader = IdRef.Resolve<Person>(LeaderId);
                return mLeader;
            }
            set { mLeader = value; LeaderId = value != null ? value.Id : 0; }
        }

        /// <summary>
        /// 部队类型的 id（存档数值）
        /// </summary>
        [JsonProperty("TroopType")]
        public int TroopTypeId;

        TroopType mTroopType;
        public TroopType TroopType
        {
            get
            {
                if (mTroopType == null && TroopTypeId > 0) mTroopType = IdRef.Resolve<TroopType>(TroopTypeId);
                return mTroopType;
            }
            set { mTroopType = value; TroopTypeId = value != null ? value.Id : 0; }
        }

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
                case (int)AbilityType.Ride:
                    return leader.RideLv * 100 + leader.Command * 120 / 100 + leader.Strength * 130 / 100;
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
