using System.Collections.Generic;
using Newtonsoft.Json;
using Sango.Core.Action;

namespace Sango.Core
{
    [JsonObject(MemberSerialization.OptIn)]

    public class ItemType : SangoObjectExtensionData
    {
        /// <summary>
        /// 主类型
        /// </summary>
        [JsonProperty] public int kind;

        /// <summary>
        /// 子类型
        /// </summary>
        [JsonProperty] public int subKind;

        /// <summary>
        /// 次类型
        /// </summary>
        [JsonProperty] public int storeKind;

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

        // ==================== 珍品（宝物 / 装备）数据字段 ====================
        //
        // 为什么定义在 ItemType 而不是 Equipment 子类：
        //   道具库由 SangoObjectMap<ItemType> 承载，反序列化走 SangoObjectMapConverter<T> 里的
        //   serializer.Deserialize<T>(reader)，T 恒为 ItemType —— 也就是说运行期**所有**道具
        //   （含宝物与装备）都是 ItemType 实例，写在 Equipment 子类上的字段永远读不到。
        //   因此品级 / 五维加成 / 特效统一放在基类，Equipment 只保留语义。

        /// <summary>
        /// 品级（宝物 / 装备：1~5，用于忠诚增减曲线与加成上限；其他道具为 0）
        /// </summary>
        [JsonProperty] public int grade;

        /// <summary>
        /// 发现权重（宝物 / 装备：搜索人才时被抽中的相对权重，0 = 不可通过搜索获得）
        /// </summary>
        [JsonProperty] public int discoverProbability;

        /// <summary>
        /// 产出限定城市 Id 列表（空 = 不限）。非空时只有这些城市能搜出本道具。
        ///
        /// 与 <see cref="discoverProvinceIds"/> 是**或**关系：两个都填就"命中任一即可"，
        /// 两个都空就是不限（默认）。判定见 <see cref="CanDiscoverAt"/>。
        /// </summary>
        [JsonProperty] public int[] discoverCityIds;

        /// <summary>
        /// 产出限定州 Id 列表（空 = 不限）。非空时只有这些州下辖的城市能搜出本道具。
        /// 州与城市的归属走 <see cref="City.province"/>（存档按 ProvinceId 解析）。
        /// </summary>
        [JsonProperty] public int[] discoverProvinceIds;

        /// <summary>
        /// 统率加成
        /// </summary>
        [JsonProperty] public int commandBonus;

        /// <summary>
        /// 武力加成
        /// </summary>
        [JsonProperty] public int strengthBonus;

        /// <summary>
        /// 智力加成
        /// </summary>
        [JsonProperty] public int intelligenceBonus;

        /// <summary>
        /// 政治加成
        /// </summary>
        [JsonProperty] public int politicsBonus;

        /// <summary>
        /// 魅力加成
        /// </summary>
        [JsonProperty] public int glamourBonus;

        /// <summary>
        /// 装备时提供 / 解锁的兵种 Id（0 = 不提供）。
        ///
        /// 【生效方式】出征解析兵种时，主将当前装备的该字段作为候选，
        /// 优先级低于玩家手动选择，高于普通兵种默认值（见 TroopTypeResolver）。
        /// 装备兵种不要求额外兵种道具，只校验势力科技。
        /// </summary>
        [JsonProperty] public int troopTypeId;

        /// <summary>
        /// 附加特技（序列化形态保持 int[]、键名不变，老存档可读）；运行期列表由 OnScenarioPrepare 解析。
        ///
        /// 【生效方式】装备 / 卸下 / 读档时由 <c>Person.InitPersonActions</c> →
        /// <c>Person.SyncExtraFeatures</c> 收集到武将的 <c>extra_feature_list</c>（不存档）：
        ///   · 判定入口：<c>Person.HasFeature</c>（是否存在）/ <c>Person.CollectEffectiveFeatures</c>（按场景取生效项），
        ///     两者都会把 FeatureList 与 extra_feature_list 合并后再按 kind 展开混合组；
        ///   · 武将本身已有同一特技时按 Id 去重，不会重复添加，卸下时也不会误删自带的那个；
        ///   · 装备特技不占剧本特技名额（maxActiveFeatureCount），总是生效；
        ///   · 不写进存档：extra_feature_list 标了 [JsonIgnore]，读档后按当前装备重建。
        /// features 里可以放混合组（kind = Mixed），其子特技按各自 kind 在对应层生效。
        /// </summary>
        [JsonProperty("features")]
        public int[] features_list;
        public SangoObjectList<Feature> features = new SangoObjectList<Feature>();

        /// <summary>
        /// 效果实体集合（JSON 数组，元素形如 { "class": "XXX", ... }，由 ActionBase.Create 工厂解析）
        /// </summary>
        [JsonProperty]
        public Newtonsoft.Json.Linq.JArray actionEntities;


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

        /// <summary>
        /// 是否宝物（数据 kind = ItemKindType.Treasure，含名马 / 宝剑 / 书籍等小类，存势力国库）
        /// </summary>
        public bool IsTreasure()
        {
            return kind == (int)ItemKindType.Treasure;
        }

        /// <summary>
        /// 是否装备（数据 kind = ItemKindType.Equipment，含武器 / 铠甲 / 马匹三类，存势力国库）
        /// </summary>
        public bool IsEquipment()
        {
            return kind == (int)ItemKindType.Equipment;
        }

        /// <summary>
        /// 是否珍品（宝物或装备）—— 只有珍品才进势力国库，可被授予 / 没收
        /// </summary>
        public bool IsRareItem()
        {
            return IsTreasure() || IsEquipment();
        }

        /// <summary>
        /// 本道具能否在指定城市被搜出（产出限定城市 / 州）。
        ///
        /// 规则：两个限定列表都为空 = 不限；任一非空则必须命中（或关系）。
        /// 用在搜索产出的候选池筛选上，见 <see cref="ForceItemDiscover.RollItem"/>。
        /// </summary>
        /// <param name="city">执行搜索的城市</param>
        /// <returns>允许在该城产出返回 true</returns>
        public bool CanDiscoverAt(City city)
        {
            if (city == null)
                return false;

            bool cityLimited = discoverCityIds != null && discoverCityIds.Length > 0;
            bool provinceLimited = discoverProvinceIds != null && discoverProvinceIds.Length > 0;

            // 都没配 = 不限（默认，也是绝大多数道具的形态）
            if (!cityLimited && !provinceLimited)
                return true;

            if (cityLimited && System.Array.IndexOf(discoverCityIds, city.Id) >= 0)
                return true;

            // 州的归属按需解析（见 City.province），解析不到就当作不命中
            if (provinceLimited && city.province != null
                && System.Array.IndexOf(discoverProvinceIds, city.province.Id) >= 0)
                return true;

            return false;
        }

        // ==================== 品级配色 ====================
        //
        // 品质色是**视觉约定**，不进剧本设置（剧本设置走数值 KV，颜色塞进去没法调也没法看）。
        // 全项目只在这里定义一张表，要改色只改这一处。
        // 下标 = grade：0 = 无品级的普通道具（兵装 / 器械 / 船），1~5 = 珍品品级。
        // 色阶走"灰 → 绿 → 蓝 → 紫 → 橙金"，与稀有度直觉一致。

        /// <summary>品级配色表（下标 = <see cref="grade"/>，0 为无品级的普通道具）</summary>
        public static readonly string[] GradeColors =
        {
            "#E8E8E8",  // 0 普通（兵装 / 器械 / 船）
            "#A9B7C6",  // 1 凡品
            "#5BD97A",  // 2 良品
            "#4FA8FF",  // 3 优品
            "#B67BFF",  // 4 极品
            "#FFB43C",  // 5 神品
        };

        /// <summary>本道具的品级颜色（十六进制串，形如 #FFB43C；越界或未配置按普通色处理）</summary>
        public string GradeColorHex
        {
            get
            {
                int g = grade;
                if (g < 0 || g >= GradeColors.Length)
                    g = 0;
                return GradeColors[g];
            }
        }

        /// <summary>
        /// 带品级颜色的道具名，供对话框 / 消息文本使用（与 <see cref="Person.ColorName"/> 同一套富文本约定）。
        /// </summary>
        public string ColorName => $"<color={GradeColorHex}>{Name}</color>";

        /// <summary>
        /// 品级星级文本（★ × grade，无品级返回「—」）。
        ///
        /// 与授予宝物详情面板的 <c>new string('★', grade)</c> 同一套显示口径，
        /// 抽到这里是为了让列表列与详情面板不会各写一套（缺字 / 上限不一致）。
        /// </summary>
        public string GradeText
        {
            get
            {
                int g = grade;
                if (g <= 0) return "—";
                if (g > 5) g = 5;
                return new string('★', g);
            }
        }

        /// <summary>
        /// 五维加成文本（只列非 0 项，形如「统率 +5   武力 +3」）。全部为 0 时返回空串。
        ///
        /// 【为什么放在这里】授予宝物详情面板与国库预览列表都要显示同一段文本，
        /// 各拼一套必然出现"一处 +5、一处 5"这类口径分叉。
        /// </summary>
        /// <returns>加成文本；无任何加成时为空串</returns>
        public string GetBonusText()
        {
            System.Text.StringBuilder sb = new System.Text.StringBuilder();
            AppendBonus(sb, "统率", commandBonus);
            AppendBonus(sb, "武力", strengthBonus);
            AppendBonus(sb, "智力", intelligenceBonus);
            AppendBonus(sb, "政治", politicsBonus);
            AppendBonus(sb, "魅力", glamourBonus);
            return sb.ToString().TrimEnd();
        }

        /// <summary>往加成文本里追加一项（0 值跳过，正数带 + 号）</summary>
        static void AppendBonus(System.Text.StringBuilder sb, string label, int value)
        {
            if (value == 0)
                return;

            if (sb.Length > 0)
                sb.Append("   ");
            sb.Append(label).Append(' ');
            if (value > 0)
                sb.Append('+');
            sb.Append(value);
        }

        /// <summary>
        /// 放入国库时是否累计数量（超过 1 表示同类可叠放；珍品每件唯一，恒为 1）
        /// </summary>
        public bool IsStackable()
        {
            return !IsRareItem();
        }

        /// <summary>
        /// 取装备栏位（<see cref="EquipmentKind"/> 值）。
        /// 装备类按 subKind 直接映射；宝物的武器 / 名马小类也可上栏（与 Person.EquipWeapon 等口径一致），
        /// 其余宝物返回 -1（随身携带，不占装备栏）。
        /// </summary>
        /// <returns>装备栏位（0=武器 1=铠甲 2=马匹），非可上栏道具返回 -1</returns>
        public int GetEquipSlot()
        {
            if (IsEquipment())
            {
                // 装备：subKind 即 EquipmentKind（0=武器 1=铠甲 2=马匹）
                if (subKind == (int)EquipmentKind.Weapon
                    || subKind == (int)EquipmentKind.Armor
                    || subKind == (int)EquipmentKind.Horse)
                {
                    return subKind;
                }
                return -1;
            }

            if (IsTreasure())
            {
                // 宝物：宝剑 / 长柄武器 / 暗器 / 宝弓 → 武器栏；名马 → 马匹栏；其余随身
                if (subKind >= (int)TreasureKind.Sword && subKind <= (int)TreasureKind.Bow)
                    return (int)EquipmentKind.Weapon;
                if (subKind == (int)TreasureKind.FamousHorse)
                    return (int)EquipmentKind.Horse;
            }

            return -1;
        }

        /// <summary>
        /// 是否需要装入装备栏才生效（装备类恒为 true；宝物的武器 / 名马小类为 true）
        /// </summary>
        public bool NeedEquipSlot()
        {
            return GetEquipSlot() >= 0;
        }

        public int TransformLimit(int limit)
        {
            if(p1 == 0)
            {
                return limit;
            }
            else
            {
                return limit * p1 / 1000;
            }
        }

        // ==================== 珍品特效（与武将特技同一套机制） ====================

        /// <summary>
        /// 剧本准备：把序列化的 features_list 解析成运行期特技列表。
        /// 与 <see cref="Person.FeatureList"/> 的解析时机一致（Person.OnScenarioPrepare）。
        /// </summary>
        public override void OnScenarioPrepare(Scenario scenario)
        {
            base.OnScenarioPrepare(scenario);
            if (features_list != null && features_list.Length > 0 && features.Count == 0)
                features.FromArray(features_list);
        }

        /// <summary>存档前回写 int[]（否则会把读档时的旧 id 存回去）</summary>
        public override void OnScenarioSave(Scenario scenario)
        {
            base.OnScenarioSave(scenario);
            features_list = features != null ? features.ToArray() : null;
        }

        /// <summary>
        /// 解析道具特效实体并装配到动作列表（与 <see cref="Feature.InitActions"/> 同一模式）。
        ///
        /// 调用时机：剧本解析 / 装备到手 / 卸下 / 授予 / 没收 / 学得或失去特技时，
        /// 由 Person 侧的唯一初始化入口 <c>Person.InitPersonActions</c> 统一重建
        /// （动作表与武将特技的 actionList 分开管理，避免互相清除）。
        /// </summary>
        /// <param name="list">目标动作列表</param>
        /// <param name="sangoObjects">动作初始化所需的关联对象（一般是武将本人）</param>
        public void InitActions(List<ActionBase> list, params SangoObject[] sangoObjects)
        {
            if (actionEntities == null) return;
            for (int i = 0; i < actionEntities.Count; i++)
            {
                Newtonsoft.Json.Linq.JObject valus = actionEntities[i] as Newtonsoft.Json.Linq.JObject;
                ActionBase action = ActionBase.Create(valus.Value<string>("class"));
                if (action != null)
                {
                    action.Init(valus, sangoObjects);
                    list.Add(action);
                }
            }
        }
    }
}
