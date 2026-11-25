using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using Sango.Core.Action;

namespace Sango.Core
{
    /// <summary>
    /// 特技「月初效果」结算系统。
    ///
    /// <para>
    /// 【为什么做成系统而不是 Action】汉土设计表里有一大批"每月开始时"的条目：
    /// 守节（忠诚设为 150）、谏议（据点武将 50% 忠诚 +3 / 俘虏 50% 忠诚 -3）、
    /// 疗疾（每月为驻城伤病武将恢复一级）、达识（势力每月技巧点 +80）……
    /// 这些效果的对象是**人物 / 城池 / 势力**，与"部队在场上"无关，
    /// 而 Action 的装配环境是部队 / 城池，人物在野外或未编队时会漏掉。
    /// 因此统一收到月度系统里：每月遍历一次在册武将，按
    /// <c>PersonMonthlyEffect</c> 配置逐条结算，一次且仅一次。
    /// </para>
    /// <para>
    /// 【与原版的关系】原版 <c>Game::on_month</c> 同样是"每月遍历人物做一遍"
    /// （s11_game_month.cpp），本系统只是把写死的那几个分支改成配置驱动。
    /// </para>
    /// <para>
    /// 【参数总表】见 <see cref="PersonMonthlyEffect"/> 的注释。
    /// </para>
    /// </summary>
    [GameSystem]
    public class FeatureMonthlyEffectSystem : GameSystem
    {
        /// <summary>本次月度结算已处理过的"势力 + 特技"组合（用于 oncePerForce 去重）</summary>
        readonly HashSet<long> onceKeys = new HashSet<long>();

        /// <summary>
        /// 遍历用的复用缓冲（不存档）。
        ///
        /// 【为什么要复用】本系统每月对**全部在册武将**跑一遍，若在循环里 new List，
        /// 就是"人数"次一次性分配（净增 GC）。系统是单例且回调是单线程串行的，
        /// 一个字段缓冲即可，无需每次分配。
        /// </summary>
        readonly List<Feature> featureBuffer = new List<Feature>(8);

        /// <summary>初始化：订阅月度更新</summary>
        public override void Init()
        {
            Name = "特技月初效果";
            // 先退订再订阅：GameSystem 是单例，重复 Init 不能把同一回调挂两次
            GameEvent.OnMonthUpdate -= OnMonthUpdate;
            GameEvent.OnMonthUpdate += OnMonthUpdate;
            GameEvent.OnTurnStart -= OnTurnStart;
            GameEvent.OnTurnStart += OnTurnStart;
        }

        /// <summary>清理事件订阅</summary>
        public override void Clear()
        {
            GameEvent.OnMonthUpdate -= OnMonthUpdate;
            GameEvent.OnTurnStart -= OnTurnStart;
            // 缓冲里装的是上一个剧本的 Feature 引用，卸载时一并丢掉
            featureBuffer.Clear();
            onceKeys.Clear();
        }

        /// <summary>月更（每 3 旬）：结算 period = 0 的条目</summary>
        /// <param name="scenario">当前剧本</param>
        void OnMonthUpdate(Scenario scenario)
        {
            Run(scenario, 0);
        }

        /// <summary>
        /// 旬更（每回合，36 次/年）：结算 period = 1 的条目。
        ///
        /// 【为什么单独一个入口】汉土设计里"每旬"和"每月"是两种节奏：
        /// 更制（军团每旬行动力 +20）、横征（每旬 +100 金）、穷经（每旬技巧点）必须按旬算，
        /// 塞进月更会慢 3 倍。两条链路共用一个 <c>Run</c>，只是 period 过滤不同。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        void OnTurnStart(Scenario scenario)
        {
            Run(scenario, 1);
        }

        /// <summary>
        /// 结算入口。
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        /// <param name="period">0 每月 / 1 每旬</param>
        void Run(Scenario scenario, int period)
        {
            if (scenario == null || scenario.personSet == null)
                return;

            onceKeys.Clear();
            featureBuffer.Clear();

            // 按武将遍历：特技挂在人身上，效果也以"持有者"为锚点
            scenario.personSet.ForEach(person =>
            {
                if (person == null || !person.IsAlive)
                    return;

                // 复用同一个缓冲：CollectEffectiveFeatures 内部只 Add，不清空，调用方负责
                featureBuffer.Clear();
                person.CollectEffectiveFeatures(-1, featureBuffer);
                for (int i = 0; i < featureBuffer.Count; i++)
                {
                    Feature feature = featureBuffer[i];
                    // hasMonthlyEffect 是剧本准备阶段算好的标志位：
                    // 有月初效果的特技是极少数，先判一次就能跳过后面整段 JSON 扫描
                    if (feature == null || !feature.hasMonthlyEffect || feature.actionEntities == null)
                        continue;

                    for (int j = 0; j < feature.actionEntities.Count; j++)
                    {
                        if (!(feature.actionEntities[j] is JObject entry))
                            continue;
                        if (entry.Value<string>("class") != PersonPassiveRule.ClassMonthlyEffect)
                            continue;
                        // period：0 每月（默认）/ 1 每旬，两条结算链各跑各的
                        if (entry.Value<int>("period") != period)
                            continue;

                        Apply(person, feature, entry);
                    }
                }
            });
        }

        /// <summary>
        /// 结算单条月初效果。
        /// </summary>
        /// <param name="owner">特技持有者</param>
        /// <param name="feature">特技（用于 oncePerForce 去重）</param>
        /// <param name="p">JSON 参数</param>
        void Apply(Person owner, Feature feature, JObject p)
        {
            int type = p.Value<int>("type");
            int value = p.Value<int>("value");
            int probability = p.Value<int>("probability");
            int scope = p.Value<int>("scope");
            int stat = p.Value<int>("stat");
            int threshold = p.Value<int>("threshold");
            int meritCost = p.Value<int>("meritCost");

            if (!Roll(probability))
                return;

            switch (type)
            {
                // 0 忠诚设值（守节：月初忠诚设为 150）
                case 0:
                    owner.loyalty = ClampLoyalty(value);
                    break;

                // 1 忠诚加减（本人）
                case 1:
                    owner.loyalty = ClampLoyalty(owner.loyalty + value);
                    break;

                // 2 功绩加减（本人）
                case 2:
                    if (value != 0)
                        owner.GainMerit(value);
                    break;

                // 3 同城武将忠诚加减（谏议 / 蠹政 / 行间）
                case 3:
                    ForEachCityPerson(owner, scope, other =>
                    {
                        other.loyalty = ClampLoyalty(other.loyalty + value);
                    });
                    break;

                // 4 吸取同城武将功绩（荫泽：每人最多 value）
                case 4:
                    ForEachCityPerson(owner, scope, other =>
                    {
                        if (other.merit <= 0)
                            return;
                        int take = System.Math.Min(other.merit, value);
                        if (take <= 0)
                            return;
                        other.GainMerit(-take);
                        owner.GainMerit(take);
                    });
                    break;

                // 5 恢复伤病（value 为恢复级数；scope 0 本人 / 1 同城）
                case 5:
                    if (scope == 0)
                        RecoverInjury(owner, value);
                    else
                        ForEachCityPerson(owner, 0, other => RecoverInjury(other, value));
                    break;

                // 6 传授能力经验（传道 / 授业 / 辨经：对低于 threshold 的同城空闲武将）
                case 6:
                    ForEachCityPerson(owner, 1, other =>
                    {
                        if (other == owner)
                            return;
                        if (GetBaseStat(other, stat) >= threshold)
                            return;
                        other.GainStatExp(stat, value);
                    });
                    break;

                // 7 消耗功绩换五维（攘功：value 为消耗功绩，提升 1 点）
                case 7:
                    if (owner.merit < meritCost || meritCost <= 0)
                        return;
                    if (!TryRaiseLowestStat(owner, stat, value))
                        return;
                    owner.GainMerit(-meritCost);
                    break;

                // 8 势力技巧点（达识 / 鸿儒 / 崇文；securityDiv > 0 时按城治安折算，见穷经）
                case 8:
                {
                    Force force = owner.BelongForce;
                    if (force == null || !force.IsAlive || value == 0)
                        return;

                    int securityDiv = p.Value<int>("securityDiv");
                    int gain = value;
                    if (securityDiv > 0)
                    {
                        City city = owner.CurrentCity;
                        if (city == null)
                            return;
                        gain = city.security / securityDiv * value;
                    }

                    if (gain == 0)
                        return;

                    // oncePerForce：同一势力内同一特技每月只结算一次（"同军团不重复"）
                    if (p.Value<bool>("oncePerForce"))
                    {
                        long key = (long)force.Id * 1000000L + feature.Id;
                        if (!onceKeys.Add(key))
                            return;
                    }

                    // GainTechniquePoint 内部有原版的入口减半与上限夹取，
                    // 这里传原始值，日志用返回值才是玩家实际看到的增量
                    int gained = force.GainTechniquePoint(gain);
                    if (gained != 0)
                        Sango.Log.Info($"*[技巧点]{owner.Name}的「{feature.Name}」使{force.Name} 技巧点 +{gained}");
                    break;
                }

                // 9 所在城治安加减（横征：每月治安 -10）
                case 9:
                {
                    City city = owner.CurrentCity;
                    if (city == null)
                        return;
                    // 走 AddSecurity：内部有 0..100 夹取，裸写字段会越界
                    city.AddSecurity(value);
                    break;
                }

                // 10 所在城兵力增加（冠冕：每月增加 魅力 × value 兵）
                case 10:
                {
                    City city = owner.CurrentCity;
                    if (city == null || value <= 0)
                        return;
                    int add = owner.Glamour * value;
                    if (add <= 0)
                        return;
                    // 走 AddTroops：内部按 TroopsLimit 夹取，裸写字段会超编
                    int before = city.troops;
                    city.AddTroops(add);
                    int real = city.troops - before;
                    if (real > 0)
                        Sango.Log.Info($"*[内政]{city.Name} 的「{feature.Name}」使兵力 +{real}");
                    break;
                }

                // 11 军团行动力加减（更制：每旬 +20）—— 建议配 period = 1
                case 11:
                {
                    City city = owner.CurrentCity;
                    Corps corps = city != null ? city.BelongCorps : null;
                    if (corps == null || value == 0)
                        return;
                    // ReduceActionPoint 是"扣减"，加行动力要传负数（与 CitySearchingExtraEffect 同口径）
                    corps.ReduceActionPoint(-value);
                    break;
                }

                // 12 所在城金 / 粮加减（横征 +100 金 / 侵帑 -50 金）
                // AddGold / AddFood 内部已做 [0, Limit] 夹取且支持负数，直接传即可
                case 12:
                {
                    City city = owner.CurrentCity;
                    if (city == null)
                        return;
                    if (value != 0)
                        city.AddGold(value);
                    int foodValue = p.Value<int>("food");
                    if (foodValue != 0)
                        city.AddFood(foodValue);
                    break;
                }
            }
        }

        /// <summary>万分比判定（0 与 10000 都视为必定）</summary>
        /// <param name="probability">概率（万分比）</param>
        static bool Roll(int probability)
        {
            if (probability <= 0 || probability >= 10000)
                return true;
            return GameRandom.Chance(probability, 10000);
        }

        /// <summary>忠诚钳制到 0..MaxLoyalty（与原版 person_add_loyalty 同口径）</summary>
        /// <param name="v">目标值</param>
        static int ClampLoyalty(int v)
        {
            if (v < 0) return 0;
            if (v > Person.MaxLoyalty) return Person.MaxLoyalty;
            return v;
        }

        /// <summary>恢复伤病（value 为恢复级数）</summary>
        /// <param name="person">目标武将</param>
        /// <param name="value">恢复级数</param>
        static void RecoverInjury(Person person, int value)
        {
            if (value <= 0 || person.injury <= 0)
                return;
            person.injury = System.Math.Max(0, person.injury - value);
        }

        /// <summary>取五维裸值（不含装备 / 特技加成）</summary>
        /// <param name="person">目标武将</param>
        /// <param name="stat">0 统率 / 1 武力 / 2 智力 / 3 政治 / 4 魅力</param>
        static int GetBaseStat(Person person, int stat)
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
        /// 提升一项五维（按当前经验条直接加经验，走正常升级流程）。
        /// </summary>
        /// <param name="person">目标武将</param>
        /// <param name="stat">属性（-1 = 取最低的一项）</param>
        /// <param name="points">提升点数</param>
        /// <returns>是否成功</returns>
        static bool TryRaiseLowestStat(Person person, int stat, int points)
        {
            if (points <= 0)
                return false;

            int index = stat;
            if (index < 0)
            {
                // 取当前值最低且未封顶的一项
                index = -1;
                int lowest = int.MaxValue;
                for (int i = 0; i < 5; i++)
                {
                    int v = GetBaseStat(person, i);
                    if (v >= 100)
                        continue;
                    if (v < lowest)
                    {
                        lowest = v;
                        index = i;
                    }
                }
                if (index < 0)
                    return false;
            }

            // 1 点属性 ≈ 一次满条经验（AttributeExpLevelNeed 的近似），
            // 这里只给经验不给整值，保持"经验条照常推进"的一致口径
            Scenario scenario = Scenario.Cur;
            if (scenario == null || scenario.Variables == null)
                return false;

            int need = scenario.Variables.AttributeExpLevelNeed;
            if (need <= 0)
                need = 250;
            person.GainStatExp(index, need * points);
            return true;
        }

        /// <summary>
        /// 遍历"同城武将"。
        /// </summary>
        /// <param name="owner">持有者</param>
        /// <param name="scope">0 同城全部 / 1 仅空闲武将 / 2 仅俘虏</param>
        /// <param name="action">处理委托</param>
        void ForEachCityPerson(Person owner, int scope, System.Action<Person> action)
        {
            City city = owner.CurrentCity;
            if (city == null || city.allPersons == null || action == null)
                return;

            for (int i = 0; i < city.allPersons.Count; i++)
            {
                Person other = city.allPersons[i];
                if (other == null || other == owner || !other.IsAlive)
                    continue;

                if (scope == 1 && !other.IsFree)
                    continue;
                if (scope == 2 && other.state != (int)PersonStateType.Prisoner)
                    continue;

                action(other);
            }
        }
    }
}
