using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 据点每月兵装 / 军马产出。
    ///
    /// <para>
    /// 【对应设计条目】良工（每月政治×5 的枪 / 戟 / 弩）、马政（每月政治×5 匹军马）、
    /// 亲羌 / 绥乌（马匹产量翻倍）、亲夷 / 绥蛮（戟 / 枪产量翻倍）、王佐（参与生产时产量×2）。
    /// </para>
    /// <para>
    /// 【时机】订阅 <c>OnCityMonthStart</c>，只处理**装配自己的那座城**。
    /// 城里的任何一名持有者都会装配一次，因此同一座城有多人持有同一特技时，
    /// 会按人数叠加 —— 与"每人每月各产一份"的设计一致；
    /// 需要"每城只算一次"的条目请配 <c>oncePerCity = true</c>。
    /// </para>
    /// <para>
    /// JSON 参数：
    /// <list type="table">
    /// <item><term>storeKind</term><description>兵装种类：1 剑 / 2 枪 / 3 戟 / 4 弩 / 5 马（= ItemStoreKindType，也是兵装 ItemType 的 Id）</description></item>
    /// <item><term>mode</term><description>0 固定 value / 1 政治 × value / 2 现有库存 × (value - 100) / 100（翻倍配 200）</description></item>
    /// <item><term>value</term><description>数值</description></item>
    /// <item><term>capByLimit</term><description>是否按仓库上限截断（默认 true）</description></item>
    /// <item><term>oncePerCity</term><description>同城同特技每月只结算一次（默认 false = 每个持有者各产一份）</description></item>
    /// <item><term>condition</term><description>可选额外条件（通用上下文，可复用 CityAttributeCheck / FactionCheck）</description></item>
    /// </list>
    /// </para>
    /// </summary>
    public class CityProductGainMonthly : CityActionBase
    {
        /// <summary>兵装种类（ItemStoreKindType）</summary>
        int storeKind;
        /// <summary>计算方式：0 固定 / 1 政治 × value / 2 现有库存按百分比</summary>
        int mode;
        /// <summary>数值</summary>
        int value;
        /// <summary>是否按仓库上限截断</summary>
        bool capByLimit = true;
        /// <summary>同城同特技每月只结算一次</summary>
        bool oncePerCity;
        /// <summary>执行者（装配时的武将）</summary>
        Person owner;
        /// <summary>本配置所属特技（<c>sangoObjects</c> 末尾由 Feature.InitActions 追加，用于 oncePerCity 去重键）</summary>
        Feature selfFeature;
        /// <summary>可选的额外条件</summary>
        Condition condition;

        /// <summary>
        /// oncePerCity 的去重键集合。
        ///
        /// 【静态的原因】同城不同持有者是**不同的 Action 实例**，要跨实例去重只能放静态。
        /// 【键的构成】<c>city.Id × 1000000 + feature.Id</c>：同一座城里"良工"和"马政"
        /// 是两个不同的特技，不能互相挡住。
        /// 【清理时机】用"年 × 12 + 月"的时间戳判定换代，跨月自动清空，
        /// 不用额外挂月度回调，也不会有"上一局残留"的问题（时间戳必然变化）。
        /// </summary>
        static int onceStamp = int.MinValue;
        static readonly System.Collections.Generic.HashSet<long> onceKeys = new System.Collections.Generic.HashSet<long>();
        /// <summary>
        /// 去重键所属的剧本。
        ///
        /// 【为什么还要判剧本】只靠"年 × 12 + 月"会在"新开局恰好停在同一日期"时
        /// 误判为同一轮而不清空键，导致当月的第一座城被上一局残留挡住。
        /// </summary>
        static Scenario onceScenario;

        /// <summary>初始化并订阅月初事件</summary>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            storeKind = p.Value<int>("storeKind");
            mode = p.Value<int>("mode");
            value = p.Value<int>("value");
            capByLimit = p.Value<bool?>("capByLimit") ?? true;
            oncePerCity = p.Value<bool>("oncePerCity");
            owner = sangoObjects != null && sangoObjects.Length > 1 ? sangoObjects[1] as Person : null;
            // Feature.InitActions 会在参数末尾追加特技自身：(City, Person, Feature)
            selfFeature = sangoObjects != null && sangoObjects.Length > 2 ? sangoObjects[2] as Feature : null;

            // 复用对象先清旧条件，避免上一次配置残留
            condition = null;
            JObject conObj = p.Value<JObject>("condition");
            if (conObj != null)
            {
                condition = Condition.Create(conObj.Value<string>("class"));
                if (condition != null)
                    condition.Init(conObj, sangoObjects);
                else
                    Sango.Log.Error($"据点兵装产出效果的 condition 类未注册：{conObj.Value<string>("class")}，已忽略该条件");
            }

            GameEvent.OnCityMonthStart += OnCityMonthStart;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnCityMonthStart -= OnCityMonthStart;
        }

        /// <summary>月初结算</summary>
        /// <param name="city">城市</param>
        /// <param name="scenario">当前剧本</param>
        void OnCityMonthStart(City city, Scenario scenario)
        {
            if (city == null || City == null || city != City || storeKind <= 0 || value == 0)
                return;

            if (condition != null)
            {
                // 城池场景没有专用的条件上下文：用通用容器把"当前城池 + 所属势力"填进去，
                // CityAttributeCheck / FactionCheck 这类条件可以直接复用（同 CityReduceDurabilityDamage）
                DefaultConditionDatabase db = new DefaultConditionDatabase
                {
                    ActionCity = city,
                    ActionForce = city.BelongForce,
                    ActionObject = city,
                };
                if (!condition.Check(db))
                    return;
            }

            int num;
            switch (mode)
            {
                case 1:
                    // 按政治折算（良工 / 马政：政治 × value）
                    // 政治读的是 Person.Politics —— 已含特技常驻修正与伤病折算，
                    // 所以"政治 +5 类"被动会自然放大产量，符合设计意图
                    num = (owner != null ? owner.Politics : 0) * value;
                    break;
                case 2:
                    // 现有库存按百分比增减（翻倍 → value = 200）
                    num = city.itemStore.GetNumber(storeKind) * (value - 100) / 100;
                    break;
                default:
                    num = value;
                    break;
            }

            if (num <= 0)
                return;

            if (oncePerCity)
            {
                // 年 × 12 + 月：跨月必然变化，借此清空上一轮的去重键
                int stamp = scenario != null && scenario.Info != null
                    ? scenario.Info.year * 12 + scenario.Info.month
                    : 0;
                if (stamp != onceStamp || !ReferenceEquals(onceScenario, scenario))
                {
                    onceStamp = stamp;
                    onceScenario = scenario;
                    onceKeys.Clear();
                }

                int featureId = selfFeature != null ? selfFeature.Id : 0;
                long key = (long)city.Id * 1000000L + featureId;
                if (!onceKeys.Add(key))
                    return;
            }

            if (capByLimit)
            {
                // 【上限口径与生产/运输完全一致】见 CityAI.cs:3676-3691：
                //   上限 = ItemType.TransformLimit(StoreLimit)；
                //   兵装四类（枪/戟/弓/马）在 ItemTypes.json 里没有 p1（= 0），
                //   所以 TransformLimit 直接返回 StoreLimit —— 即"每类各能存到整个仓库上限"；
                //   船 / 攻城器械 p1 = 10 时才按 1% 折算。
                // 道具表里查不到时退回 StoreLimit，与 CityAIOutpost.cs:679 同口径。
                ItemType productType = scenario != null ? scenario.GetObject<ItemType>(storeKind) : null;
                int capacity = productType != null
                    ? productType.TransformLimit(city.StoreLimit)
                    : city.StoreLimit;
                int room = capacity - city.itemStore.GetNumber(storeKind);
                if (room <= 0)
                    return;
                if (num > room)
                    num = room;
            }

            city.itemStore.Add(storeKind, num);
            Sango.Log.Info($"*[内政]{city.Name} 的「{owner?.Name}」月度兵装产出：种类 {storeKind} +{num}");
        }
    }

    /// <summary>
    /// 搜索额外奖励。
    ///
    /// <para>
    /// 【对应设计条目】得财（50% 额外 50 金）、察弊 / 横暴（50% 治安 ±3）、
    /// 苛敛（50% 50 金 + 150 粮，治安 -3）、抚众（33% 按魅力加兵）、
    /// 营私（33% 自身忠诚 -3）、简政（33% 军团行动力 +5）、煽乱（30% 治安 -3 且守军 -100）、
    /// 清议（额外技巧点）。
    /// </para>
    /// <para>
    /// 【时机】订阅 <c>OnCityJobSearchingSettled</c>：搜索演出**结束之后**抛出，
    /// 带结果码（0 人才 / 正数 金钱 / -2 宝物 / -1 空手），
    /// 因此额外奖励不会与主结果的演出抢镜头。
    /// </para>
    /// <para>
    /// JSON 参数：probability（万分比，0/10000 = 必定）、gold、food、security、troops、
    /// troopsByGlamour（魅力 × 该系数 加兵）、loyalty（执行者忠诚）、actionPoint（军团行动力）、
    /// techniquePoint（势力技巧点）。
    /// </para>
    /// </summary>
    public class CitySearchingExtraEffect : CityActionBase
    {
        /// <summary>触发概率（万分比）</summary>
        int probability;
        /// <summary>额外资金</summary>
        int gold;
        /// <summary>额外兵粮</summary>
        int food;
        /// <summary>治安增减</summary>
        int security;
        /// <summary>守军增减</summary>
        int troops;
        /// <summary>按魅力折算的守军增量系数</summary>
        int troopsByGlamour;
        /// <summary>执行者忠诚增减</summary>
        int loyalty;
        /// <summary>军团行动力增减</summary>
        int actionPoint;
        /// <summary>势力技巧点</summary>
        int techniquePoint;
        /// <summary>装配时的武将（执行者校验用）</summary>
        Person owner;

        /// <summary>初始化并订阅搜索结算事件</summary>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            base.Init(p, sangoObjects);

            probability = p.Value<int>("probability");
            gold = p.Value<int>("gold");
            food = p.Value<int>("food");
            security = p.Value<int>("security");
            troops = p.Value<int>("troops");
            troopsByGlamour = p.Value<int>("troopsByGlamour");
            loyalty = p.Value<int>("loyalty");
            actionPoint = p.Value<int>("actionPoint");
            techniquePoint = p.Value<int>("techniquePoint");
            owner = sangoObjects != null && sangoObjects.Length > 1 ? sangoObjects[1] as Person : null;

            GameEvent.OnCityJobSearchingSettled += OnCityJobSearchingSettled;
        }

        /// <summary>退订事件（Action 被清理时必须调用）</summary>
        public override void Clear()
        {
            GameEvent.OnCityJobSearchingSettled -= OnCityJobSearchingSettled;
        }

        /// <summary>搜索结算后的额外奖励</summary>
        /// <param name="person">执行搜索的武将</param>
        /// <param name="city">所在城市</param>
        /// <param name="result">主结果码</param>
        /// <param name="found">找到的武将</param>
        void OnCityJobSearchingSettled(Person person, City city, int result, Person found)
        {
            if (city == null || City == null || city != City || person == null)
                return;

            // 只处理"装配自己的那个武将"执行的那一次搜索：
            // 同城多人持有同一特技时不会互相触发
            if (owner != null && person != owner)
                return;

            if (probability > 0 && probability < 10000 && !GameRandom.Chance(probability, 10000))
                return;

            if (gold != 0)
                city.AddGold(gold);
            if (food != 0)
                city.AddFood(food);
            if (security != 0)
                city.AddSecurity(security);
            if (troops != 0)
                city.AddTroops(troops);
            if (troopsByGlamour > 0)
                city.AddTroops(person.Glamour * troopsByGlamour);
            if (loyalty != 0)
                person.loyalty = System.Math.Max(0,
                    System.Math.Min(Person.MaxLoyalty, person.loyalty + loyalty));
            if (actionPoint != 0)
                city.BelongCorps?.ReduceActionPoint(-actionPoint);
            if (techniquePoint != 0)
                city.BelongForce?.GainTechniquePoint(techniquePoint);

            Sango.Log.Info($"*[内政]{city.Name} 的{person.Name}搜索触发额外奖励"
                           + $"（金{gold} 粮{food} 治安{security} 兵{troops}）");
        }
    }
}
