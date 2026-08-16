using TKNewtonsoft.Json;
using Sango.Render;
using System;
using System.Collections.Generic;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]
    public class PersonLib : SangoObject
    {
        public override SangoObjectType ObjectType { get { return SangoObjectType.Person; } }
        public override string Name => $"{familyName}{giveName}";

        public string ColorName => $"<color=#7CCADB>{Name}</color>";

        public bool isAppend = false;

        /// <summary>
        /// 姓
        /// </summary>
        public int familyNameID;
        [JsonProperty] public string familyName;

        /// <summary>
        /// 名
        /// </summary>
        public int giveNameID;
        [JsonProperty] public string giveName;

        /// <summary>
        /// 字
        /// </summary>
        public int nickNameID;
        [JsonProperty] public string nickName;

        /// <summary>
        /// 身平/列傳
        /// </summary>
        [JsonProperty] public string description;

        /// <summary>
        /// 头像id
        /// </summary>
        [JsonProperty] public int headIconID;

        /// <summary>
        /// 立绘id(弃用)
        /// </summary>
        [JsonProperty] public string imageID;

        /// <summary>
        /// 立绘id
        /// </summary>
        [JsonProperty] public string image;

        /// <summary>
        /// 立绘id
        /// </summary>
        [JsonProperty] public string image_old;

        /// <summary>
        /// 性别
        /// </summary>
        [JsonProperty] public int sex;

        /// <summary>
        /// 登场年份
        /// </summary>
        [JsonProperty] public int yearAvailable;

        /// <summary>
        /// 出生年
        /// </summary>
        [JsonProperty] public int yearBorn;

        /// <summary>
        /// 死亡年
        /// </summary>
        [JsonProperty] public int yearDead;

        /// <summary>
        /// 相性
        /// </summary>
        [JsonProperty] public int compatibility;

        /// <summary>
        /// 身分
        /// </summary>
        [JsonProperty]
        public int state;

        /// <summary>
        /// 性格
        /// </summary>
        [JsonProperty]
        public int personality;

        /// <summary>
        /// 义理
        /// </summary>
        [JsonProperty]
        public int argumentation;

        /// <summary>
        /// 音聲
        /// </summary>
        [JsonProperty]
        public int voice;

        /// <summary>
        /// 語氣
        /// </summary>
        [JsonProperty]
        public int tone;

        /// <summary>
        /// 汉室态度
        /// </summary>
        [JsonProperty]
        public int kanshitsu;

        /// <summary>
        /// 理想
        /// </summary>
        [JsonProperty]
        public int ideal;

        /// <summary>
        /// 才幹
        /// </summary>
        [JsonProperty]
        public int talent;

        /// <summary>
        /// 功绩
        /// </summary>
        [JsonProperty] public int merit;

        /// <summary>
        /// 体力
        /// </summary>
        [JsonProperty] public int stamina;

        /// <summary>
        /// 经验
        /// </summary>
        [JsonProperty] 
        public int Exp { get; private set; }

        /// <summary>
        /// 等级
        /// </summary>
        [JsonProperty]
        public int Level;

        /// <summary>
        /// 统御
        /// </summary>
        [JsonProperty]
        public int command;

        /// <summary>
        /// 武力
        /// </summary>
        [JsonProperty]
        public int strength;

        /// <summary>
        /// 智力
        /// </summary>
        [JsonProperty]
        public int intelligence;

        /// <summary>
        /// 政治
        /// </summary>
        [JsonProperty]
        public int politics;

        /// <summary>
        /// 魅力
        /// </summary>
        [JsonProperty]
        public int glamour;

        /// <summary>
        /// 能力成長類型（對應 AttributeChangeType.Id）
        /// </summary>
        [JsonProperty]
        public int attributeChangeType;

        /// <summary>
        /// 能力持續類型（0=長，1=短）
        /// </summary>
        [JsonProperty]
        public int attributeDuration;

        /// <summary>
        /// 血缘
        /// </summary>
        [JsonProperty] 
        public int consanguinity;

        /// <summary>
        /// 父亲
        /// </summary>
        [JsonProperty]
        public int Father { get; set; }

        /// <summary>
        /// 母亲
        /// </summary>
        [JsonProperty]
        public int Mother { get; set; }

        /// <summary>
        /// 配偶
        /// </summary>
        [JsonProperty]
        public int[] SpouseList { get; set; }

        /// <summary>
        /// 兄弟
        /// </summary>
        [JsonProperty]
        public int Brother { get; set; }

        /// <summary>
        /// 兄弟
        /// </summary>
        public int[] BrotherList;

        /// <summary>
        /// 義兄弟
        /// </summary>
        [JsonProperty]
        public int[] swornBrotherList { get; set; }

        /// <summary>
        /// 喜欢武将
        /// </summary>
        [JsonProperty]
        public int[] LikePersonList { get; set; }

        /// <summary>
        /// 厌恶武将
        /// </summary>
        [JsonProperty]
        public int[] HatePersonList { get; set; }

        /// <summary>
        /// 矛
        /// </summary>
        [JsonProperty]
        public int spearLv;

        /// <summary>
        /// 戟
        /// </summary>
        [JsonProperty]
        public int halberdLv;

        /// <summary>
        /// 弓弩
        /// </summary>
        [JsonProperty]
        public int crossbowLv;

        /// <summary>
        /// 骑
        /// </summary>
        [JsonProperty]
        public int rideLv;

        /// <summary>
        /// 水军
        /// </summary>
        [JsonProperty]
        public int waterLv;

        /// <summary>
        /// 器械
        /// </summary>
        [JsonProperty]
        public int machineLv;

        /// <summary>
        /// 武将特性
        /// </summary>
        [JsonProperty]
        public int[] FeatureList { get; set; }

        public int targetShortPersonId;
        public int BelongCity(ShortScenario scenario)
        {
            if (targetShortPersonId > 0)
                return scenario.personSet[targetShortPersonId].BelongCity;
            return 0;
        }

        public int BelongForce(ShortScenario scenario)
        {
            if (targetShortPersonId > 0)
                return scenario.personSet[targetShortPersonId].BelongForce;
            return 0;
        }

        public int BelongCorps;

    }
}
