using Sango.Core.Player;

namespace Sango.Core
{
    /// <summary>
    /// 技巧点「月度自然增长」——对齐原版 <c>Game::on_month</c> 里的三条固定产出
    /// （s11_game_month.cpp:45-72）。
    ///
    /// <para>
    /// 原版每月会给三类势力加技巧点（括号内是入口减半后的实际到手值，
    /// 减半规则见 <see cref="Force.GainTechniquePoint"/>）：
    /// <list type="number">
    /// <item>皇帝所在城池的势力 +5（实际 +2）—— s11_game_month.cpp:45-51</item>
    /// <item>每座已完工的「铜雀台」所属势力 +10（实际 +5）—— s11_game_month.cpp:52-61</item>
    /// <item>玉玺持有者所在势力 +5（实际 +2）—— s11_game_month.cpp:62-72</item>
    /// </list>
    /// </para>
    ///
    /// <para>
    /// 【本项目实现差异】
    /// · 玉玺：已实现（ItemTypes.json 的 66 号 = 玉玺，见
    ///   <see cref="ItemGrantRules.SpecialItemIds"/>）；
    /// · 皇帝城：本项目没有 Open311 的 <c>get_emperor_city_id()</c> 概念（献帝所在城），未实现；
    /// · 铜雀台：本项目没有该设施类型（City.cs 的城陷注释里也记了这一点），未实现。
    /// 后两条属于"功能缺失"，不是数值偏差，等对应系统落地后再补。
    /// </para>
    /// </summary>
    [GameSystem]
    public class TechniquePointMonthlyGain : GameSystem
    {
        /// <summary>
        /// 玉玺在 ItemTypes.json 里的 Id。
        /// 出处：<see cref="ItemGrantRules.SpecialItemIds"/> 的第一个元素
        /// （对应 Open311 的 <c>ItemType_ImperialSeal</c>）。
        /// </summary>
        const int ImperialSealItemTypeId = 66;

        /// <summary>玉玺持有者势力每月的技巧点（原版原始值，入口会减半）</summary>
        const int ImperialSealTechPoint = 5;

        public override void Init()
        {
            Name = "技巧点月度增长";
            // 先退订再订阅：GameSystem 是单例，重复 Init 不能把同一回调挂两次
            GameEvent.OnMonthUpdate -= OnMonthUpdate;
            GameEvent.OnMonthUpdate += OnMonthUpdate;
        }

        public override void Clear()
        {
            GameEvent.OnMonthUpdate -= OnMonthUpdate;
        }

        /// <summary>
        /// 月度结算：玉玺在谁身上，就给谁所属势力加技巧点。
        ///
        /// <para>
        /// 原版是按 <c>ItemId_ImperialSeal</c> 直接取道具再反查持有者
        /// （<c>imperial_seal->get_owner_person_id()</c>）；本项目道具挂在
        /// <c>Person.itemStore</c> 上、没有全局"道具 Id → 持有者"索引，
        /// 因此改为遍历武将找到持有玉玺的那一位 —— 结果等价，代价是一次全武将扫描（每月一次）。
        /// </para>
        /// </summary>
        /// <param name="scenario">当前剧本</param>
        void OnMonthUpdate(Scenario scenario)
        {
            if (scenario == null || scenario.CommonData == null || scenario.personSet == null)
                return;

            ItemType imperialSeal = scenario.CommonData.ItemTypes.Get(ImperialSealItemTypeId);
            if (imperialSeal == null)
                return;

            scenario.personSet.ForEach(person =>
            {
                if (person == null || !person.IsAlive)
                    return;
                if (person.itemStore == null || person.itemStore.GetNumber(imperialSeal) <= 0)
                    return;

                Force force = person.BelongForce;
                if (force == null || !force.IsAlive)
                    return;

                // 返回值是入口减半 + 上限夹取后的**实际**增量，日志用它才和左上角数值一致
                int gained = force.GainTechniquePoint(ImperialSealTechPoint);
                if (gained != 0)
                    Sango.Log.Info($"*[技巧点]玉玺在{person.Name}身上，{force.Name} 月度技巧点 +{gained}");
            });
        }
    }
}
