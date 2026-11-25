using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace Sango.Core
{
    /// <summary>
    /// 人物级常驻修正类型（索引 <see cref="Person.featureModifiers"/>）。
    ///
    /// <para>
    /// 与"五维加成"不同，这些修正不改人物面板，而是改**某条业务链的数值**：
    /// 交易收益、设施毁损返还、灾害免疫、异常状态回合数。
    /// 它们都只做一次判定、读取点分散在城池 / 建筑 / 状态系统里，因此统一收进一个包。
    /// </para>
    /// </summary>
    public enum FeatureModifierType : int
    {
        /// <summary>交易收益（百分比，买粮得到的粮 / 卖粮得到的金都乘它）—— 通商、平籴</summary>
        TradeGain = 0,

        /// <summary>据点内设施被摧毁时返还的建造资源（百分比）—— 清野</summary>
        BuildingRefund = 1,

        /// <summary>灾害免疫（非 0 即免疫疫病 / 蝗灾）—— 风水</summary>
        DisasterImmune = 2,

        /// <summary>本方施加的异常状态回合数加成（绝对值）—— 镇节一类"控制更久"</summary>
        BuffTurnsAdd = 3,
    }
}

namespace Sango.Core.Action
{
    /// <summary>
    /// 人物**常驻被动**（不依赖部队）的解析与应用。
    ///
    /// <para>
    /// 【为什么需要这一层】<see cref="TroopAddPersonAttribute"/> 只在
    /// <c>Troop.CalculateAttribute</c> 里生效，服务的是"战场上按条件改五维"
    /// （兵力越低统率越高等）。但汉土设计表里有大量**纯常驻**的条目：
    /// "政治增加 5 点""智力 -10""五维经验翻倍"…… 这些在**武将不出征**时
    /// 也必须生效（内政产出、登用概率、搜索、单挑基础值都读 <c>Person.Politics</c> 等）。
    /// </para>
    /// <para>
    /// 【实现方式】这些条目没有事件、没有触发点，本质是"配置 → 数值"的静态映射，
    /// 因此不做运行时 Action 订阅，而是由 <see cref="Person.RebuildPersonPassive"/>
    /// 在**特技集合变化时**（读档 / 学特技 / 换装 / 任免）遍历一次 actionEntities 重算。
    /// 重算前先把上一轮结果清零，因此天然可逆，不存在"卸了还在"。
    /// </para>
    /// <para>
    /// 【与部队级的关系】部队级 <see cref="TroopAddPersonAttribute"/> 在人物基线之上
    /// 二次累加，<c>ClearFeatureAttributeBonus</c> 会回落到本层算出的基线值。
    /// </para>
    /// </summary>
    public static class PersonPassiveRule
    {
        /// <summary>JSON 里的动作类名：五维常驻加减</summary>
        public const string ClassAddAttribute = "PersonAddAttribute";

        /// <summary>JSON 里的动作类名：经验获取倍率</summary>
        public const string ClassAddExpFactor = "PersonAddExpFactor";

        /// <summary>JSON 里的动作类名：常驻修正包（交易 / 返还 / 免疫 / 状态回合）</summary>
        public const string ClassAddModifier = "PersonAddModifier";

        /// <summary>JSON 里的动作类名：月初 / 每旬结算效果</summary>
        public const string ClassMonthlyEffect = "PersonMonthlyEffect";

        /// <summary>
        /// 重算单个武将的常驻被动。
        /// </summary>
        /// <param name="person">目标武将</param>
        public static void Rebuild(Person person)
        {
            if (person == null)
                return;

            // ① 先回到"没有任何被动"的状态，后面按当前特技重新累加
            person.baseFeatureCommand = 0;
            person.baseFeatureStrength = 0;
            person.baseFeatureIntelligence = 0;
            person.baseFeaturePolitics = 0;
            person.baseFeatureGlamour = 0;
            person.featureExpFactor = 100;
            person.featureAttributeExpFactor = 100;
            person.featureAbilityExpFactor = 100;
            person.featureTeamExpShare = 0;
            if (person.featureModifiers == null)
                person.featureModifiers = new int[8];
            else
                System.Array.Clear(person.featureModifiers, 0, person.featureModifiers.Length);

            // ② 取**全部** kind 的生效特技（常驻被动不受环境 kind 过滤，
            //    kindMask 传 -1 表示所有位都命中，与汉土人口事件的做法一致）
            //
            // 【零分配】缓冲区挂在 Person 上复用（本方法在每次换装 / 学特技 / 读档时都会跑，
            // 若在这里 new List 就是"人数 × 事件数"次丢弃分配）
            List<Feature> features = person.tempPassiveFeatures;
            if (features == null)
            {
                features = new List<Feature>(8);
                person.tempPassiveFeatures = features;
            }
            features.Clear();
            person.CollectEffectiveFeatures(-1, features);

            for (int i = 0; i < features.Count; i++)
            {
                Feature feature = features[i];
                // hasPersonPassive 是剧本准备阶段算好的标志位：
                // 绝大多数特技没有常驻被动，先判一次就能跳过整段 JSON 扫描
                if (feature == null || !feature.hasPersonPassive || feature.actionEntities == null)
                    continue;

                for (int j = 0; j < feature.actionEntities.Count; j++)
                {
                    if (!(feature.actionEntities[j] is JObject entry))
                        continue;

                    string cls = entry.Value<string>("class");
                    if (string.IsNullOrEmpty(cls))
                        continue;

                    if (cls == ClassAddAttribute)
                        ApplyAttribute(person, entry);
                    else if (cls == ClassAddExpFactor)
                        ApplyExpFactor(person, entry);
                    else if (cls == ClassAddModifier)
                        ApplyModifier(person, entry);
                }
            }

            // ③ 基线算完后落到"当前有效值"，让**非部队场景**立刻读到正确数值
            person.ClearFeatureAttributeBonus();
        }

        /// <summary>
        /// 五维常驻加减。
        /// </summary>
        /// <param name="person">目标武将</param>
        /// <param name="p">JSON 参数：stat / mode / value</param>
        static void ApplyAttribute(Person person, JObject p)
        {
            int stat = p.Value<int>("stat");
            int mode = p.Value<int>("mode");
            int value = p.Value<int>("value");

            // mode = 1 表示按"人物原始值"的百分比算，基准取不带装备 / 不带被动的裸值，
            // 避免两个百分比被动互相放大（A 先算完 B 再按 A 的结果算）
            int add = mode == 1 ? GetBare(person, stat) * value / 100 : value;
            if (add == 0)
                return;

            switch (stat)
            {
                case 1: person.baseFeatureStrength += add; break;
                case 2: person.baseFeatureIntelligence += add; break;
                case 3: person.baseFeaturePolitics += add; break;
                case 4: person.baseFeatureGlamour += add; break;
                default: person.baseFeatureCommand += add; break;
            }
        }

        /// <summary>取武将某项五维的裸值（不含装备与特技加成）</summary>
        /// <param name="person">目标武将</param>
        /// <param name="stat">0 统率 / 1 武力 / 2 智力 / 3 政治 / 4 魅力</param>
        static int GetBare(Person person, int stat)
        {
            switch (stat)
            {
                case 1: return person.strength.Value;
                case 2: return person.intelligence.Value;
                case 3: return person.politics.Value;
                case 4: return person.glamour.Value;
                default: return person.command.Value;
            }
        }

        /// <summary>
        /// 经验获取倍率。
        /// </summary>
        /// <param name="person">目标武将</param>
        /// <param name="p">JSON 参数：type / mode / value</param>
        static void ApplyExpFactor(Person person, JObject p)
        {
            int type = p.Value<int>("type");
            int mode = p.Value<int>("mode");
            int value = p.Value<int>("value");

            // mode = 0 乘算（可叠加多个来源）/ 1 直接设值（覆盖之前的结果）
            switch (type)
            {
                case 1:
                    person.featureAttributeExpFactor = mode == 1
                        ? value
                        : person.featureAttributeExpFactor * value / 100;
                    break;
                case 2:
                    person.featureAbilityExpFactor = mode == 1
                        ? value
                        : person.featureAbilityExpFactor * value / 100;
                    break;
                case 3:
                    // 队友经验共享（持衡 / 授业 / 辨经 / 国士）：
                    // 取最大值而不是累加 —— 两个"共享 50%"叠成 100% 会让队友白拿双份
                    if (value > person.featureTeamExpShare)
                        person.featureTeamExpShare = value;
                    break;
                default:
                    person.featureExpFactor = mode == 1
                        ? value
                        : person.featureExpFactor * value / 100;
                    break;
            }

            if (person.featureExpFactor < 0) person.featureExpFactor = 0;
            if (person.featureAttributeExpFactor < 0) person.featureAttributeExpFactor = 0;
            if (person.featureAbilityExpFactor < 0) person.featureAbilityExpFactor = 0;
        }

        /// <summary>
        /// 常驻修正包（交易收益 / 设施返还 / 灾害免疫 / 状态回合）。
        /// </summary>
        /// <param name="person">目标武将</param>
        /// <param name="p">JSON 参数：type（<see cref="FeatureModifierType"/>）、value、mode（0 累加 / 1 设值）</param>
        static void ApplyModifier(Person person, JObject p)
        {
            int type = p.Value<int>("type");
            int mode = p.Value<int>("mode");
            int value = p.Value<int>("value");

            if (person.featureModifiers == null)
                person.featureModifiers = new int[8];
            if (type < 0 || type >= person.featureModifiers.Length)
            {
                Sango.Log.Warning($"*[特技]{person.Name} 的 PersonAddModifier 配置了未知修正类型 {type}，已忽略");
                return;
            }

            person.featureModifiers[type] = mode == 1
                ? value
                : person.featureModifiers[type] + value;
        }
    }

    /// <summary>
    /// 五维常驻加减（配置声明）。
    ///
    /// <para>
    /// 本类**不做事件订阅**：数值由 <see cref="PersonPassiveRule.Rebuild"/> 在
    /// 特技集合变化时直接扫描 JSON 求值。注册在这里只是为了让
    /// <c>ActionBase.Create</c> 能成功创建实例（否则解析时会报"动作类未注册"）。
    /// </para>
    /// <para>
    /// JSON 参数：stat（0 统率 / 1 武力 / 2 智力 / 3 政治 / 4 魅力）、
    /// mode（0 加算 / 1 按裸值百分比）、value。
    /// </para>
    /// </summary>
    public class PersonAddAttribute : ActionBase
    {
        /// <summary>初始化（无事件订阅，数值由 <see cref="PersonPassiveRule"/> 统一求值）</summary>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
        }

        /// <summary>清理（无订阅可退）</summary>
        public override void Clear()
        {
        }

        /// <summary>执行（常驻被动不参与触发器链）</summary>
        public override void Execute(Trigger trigger)
        {
        }
    }

    /// <summary>
    /// 常驻修正包（配置声明）。
    ///
    /// <para>
    /// JSON 参数：type（见 <see cref="FeatureModifierType"/>）、value、mode（0 累加 / 1 设值）。
    /// 数值由 <see cref="PersonPassiveRule.Rebuild"/> 统一求值进
    /// <see cref="Person.featureModifiers"/>，本类不订阅事件。
    /// </para>
    /// <para>
    /// 【取值口径】读取方一律取"本城 / 本队范围内的最大值"，而不是求和 ——
    /// 这类修正都是"有或没有"的能力，两人同时持有不该翻倍。
    /// </para>
    /// </summary>
    public class PersonAddModifier : ActionBase
    {
        /// <summary>初始化（无事件订阅）</summary>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
        }

        /// <summary>清理（无订阅可退）</summary>
        public override void Clear()
        {
        }

        /// <summary>执行（常驻修正不参与触发器链）</summary>
        public override void Execute(Trigger trigger)
        {
        }
    }

    /// <summary>
    /// 月初（月度）结算效果（配置声明）。
    ///
    /// <para>
    /// 【为什么不在本类里订阅事件】月度效果的结算主体是
    /// <see cref="FeatureMonthlyEffectSystem"/>：它每月遍历一次在册武将，
    /// 保证"人在野外 / 未编队"时也能结算。若在这里订阅
    /// <c>OnMonthUpdate</c>，同一个特技会因为装配环境不同（部队 / 城池 / 全量）
    /// 被实例化多次，一次月度结算里重复触发 N 遍。
    /// </para>
    /// <para>
    /// JSON 参数：type（效果类型，见下）、value、probability（万分比）、scope、stat、
    /// threshold、meritCost、securityDiv、oncePerForce。
    /// <list type="table">
    /// <item><term>0</term><description>忠诚设值（value）—— 守节</description></item>
    /// <item><term>1</term><description>忠诚加减（本人）</description></item>
    /// <item><term>2</term><description>功绩加减（本人）—— 清节</description></item>
    /// <item><term>3</term><description>同城武将忠诚加减（scope 0 全部 / 1 空闲 / 2 俘虏）—— 谏议、蠹政</description></item>
    /// <item><term>4</term><description>吸取同城武将功绩，每人最多 value —— 荫泽</description></item>
    /// <item><term>5</term><description>恢复伤病 value 级（scope 0 本人 / 其他 同城）—— 疗疾、济世</description></item>
    /// <item><term>6</term><description>向同城空闲武将传授能力经验（stat / value / threshold）—— 传道、授业、辨经</description></item>
    /// <item><term>7</term><description>消耗 meritCost 功绩提升 stat 一点（stat = -1 取最低项）—— 攘功</description></item>
    /// <item><term>8</term><description>势力技巧点 value（securityDiv &gt; 0 时按"城治安 ÷ securityDiv"折算，oncePerForce 去重）—— 达识、穷经</description></item>
    /// <item><term>9</term><description>所在城治安加减 —— 横征</description></item>
    /// <item><term>10</term><description>所在城兵力增加 魅力 × value —— 冠冕</description></item>
    /// <item><term>11</term><description>军团行动力加减 —— 更制（配 period = 1）</description></item>
    /// <item><term>12</term><description>所在城金 / 粮加减（value 金、food 粮）—— 横征、侵帑</description></item>
    /// </list>
    /// </para>
    /// <para>
    /// 【period】0 = 每月（默认，由 <c>OnMonthUpdate</c> 结算）/ 1 = 每旬（由 <c>OnTurnStart</c> 结算）。
    /// 两条链路各自遍历一次，条目只会被自己那一档结算，不会重复。
    /// </para>
    /// </summary>
    public class PersonMonthlyEffect : ActionBase
    {
        /// <summary>初始化（无事件订阅，结算在 <see cref="FeatureMonthlyEffectSystem"/>）</summary>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
        }

        /// <summary>清理（无订阅可退）</summary>
        public override void Clear()
        {
        }

        /// <summary>执行（月度效果不参与触发器链）</summary>
        public override void Execute(Trigger trigger)
        {
        }
    }

    /// <summary>
    /// 经验获取倍率（配置声明）。
    ///
    /// <para>
    /// JSON 参数：type（0 武将等级经验 / 1 能力经验 / 2 兵种适性经验）、
    /// mode（0 乘算叠加 / 1 直接设值）、value（百分比，100 = 不变，200 = 翻倍）。
    /// </para>
    /// <para>
    /// 取值落点：<c>Person.featureExpFactor</c> / <c>featureAttributeExpFactor</c> /
    /// <c>featureAbilityExpFactor</c>，由 GainExp / GainStatExp / GainAbilityExp 读取。
    /// </para>
    /// </summary>
    public class PersonAddExpFactor : ActionBase
    {
        /// <summary>初始化（无事件订阅）</summary>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
        }

        /// <summary>清理（无订阅可退）</summary>
        public override void Clear()
        {
        }

        /// <summary>执行（常驻被动不参与触发器链）</summary>
        public override void Execute(Trigger trigger)
        {
        }
    }
}
