using System.Collections.Generic;

namespace Sango.Core
{
    /// <summary>
    /// 俘虏处置上下文（对应 Open311 <c>CaptureContext</c> 中"裁决"需要的那部分）。
    ///
    /// 【为什么要上下文对象】原版 `<c>horyo_shoguu</c>` 被两个地方调用：
    ///   · 据点陷落 —— s11_sys_event_kanraku.cpp:263
    ///   · 部队击破 —— s11_sys_scen_unit.cpp 的击破分支
    /// 两者只在"登用落点 / 收押去处 / 可用资金"上不同，六道闸门的顺序与公式完全一致。
    /// 本项目把差异收进委托，把顺序与公式收进 <see cref="HoryoShoguu"/>，
    /// 避免两条路径各写一份判定后逐渐漂移（原先部队击破路径就只有"登用 / 收押"两步）。
    /// </summary>
    public class HoryoShoguuContext
    {
        /// <summary>裁决方势力（原版 by_force）</summary>
        public Force ByForce;
        /// <summary>裁决方君主（原版 by_kunshu）；为空时跳过"憎恶处斩"与 shodan2</summary>
        public Person ByKunshu;
        /// <summary>攻方可动用的资金（原版 get_gold(self,1) + get_gold(self,0)）</summary>
        public int AvailableGold;
        /// <summary>攻方成员俸禄合计（原版 get_pay_sum）</summary>
        public int PaySum;
        /// <summary>取收押目的地当前已关押的俘虏数（原版 get_location_person_list 的俘虏数），随收押实时增长</summary>
        public System.Func<int> GetCaptiveCount;
        /// <summary>
        /// 攻方是否有关押俘虏的据点。
        /// 原版 `hobaku()` 在"部队击破"分支里要求攻方主将有所属据点，
        /// 否则直接返回 false（:233-239）；据点陷落分支天然有据点，恒为 true。
        /// </summary>
        public bool HasDetainFacility = true;

        /// <summary>登用：返回是否成功（成功者已改属，后续不再处置）</summary>
        public System.Func<Person, bool> TryRecruit;
        /// <summary>收押：把俘虏关进目的地（城 or 部队）</summary>
        public System.Action<Person> Detain;
        /// <summary>释放：送回本国据点（对应 person_escape）</summary>
        public System.Action<Person> Release;
        /// <summary>处斩：参数为（俘虏, 下令君主）</summary>
        public System.Action<Person, Person> Execute;
    }

    /// <summary>
    /// 俘虏裁决（对应 Open311 `System::com_judgement` / `shodan2` / `hobaku`）。
    ///
    /// 【原版出处】
    ///   com_judgement —— s11_sys_event_capture.cpp:250-287
    ///   shodan2       —— 同文件 :158-199
    ///   hobaku        —— 同文件 :201-248
    ///
    /// 【六道闸门顺序（严格照搬，不可调换）】
    /// <code>
    /// ① 君主憎恶此人     → 处斩   （:254-259）
    /// ② 登用判定通过     → 加入我方（:260-265）
    /// ③ shodan2 掷中     → 处斩   （:266-271）
    /// ④ 势力尚存的君主   → 释放   （:272-277）
    /// ⑤ 能够关押         → 收押   （:278-283）
    /// ⑥ 其余             → 释放   （:284-285）
    /// </code>
    /// ③ 在 ② 之后是原版语义：只有"登用失败"的人才可能被斩。
    /// </summary>
    public static class HoryoShoguu
    {
        /// <summary>
        /// 原版 `horyo_pay_`：每名俘虏的关押花费。
        /// 与 <see cref="City.GoldCost"/> 的"每名俘虏 100 金/月"同一口径。
        /// </summary>
        public const int HoryoPay = 100;

        /// <summary>
        /// 批量裁决（对应 `com_judgement` 的 for 循环）。
        /// 只读快照，不改动调用方的列表。
        /// </summary>
        /// <param name="prisoners">待裁决的俘虏</param>
        /// <param name="ctx">裁决上下文</param>
        public static void AiJudgeAll(List<Person> prisoners, HoryoShoguuContext ctx)
        {
            if (prisoners == null || ctx == null)
                return;

            // 快照：处置会改势力 / 名册 / 城市，不能边遍历边读源列表
            List<Person> persons = new List<Person>(prisoners);
            for (int i = 0; i < persons.Count; i++)
            {
                Person person = persons[i];
                if (person == null || person.IsDead)
                    continue;
                AiJudge(person, ctx);
            }
        }

        /// <summary>
        /// 单个俘虏的裁决（六道闸门，见类注释）。
        /// </summary>
        /// <param name="prisoner">待裁决的俘虏</param>
        /// <param name="ctx">裁决上下文</param>
        public static void AiJudge(Person prisoner, HoryoShoguuContext ctx)
        {
            if (prisoner == null || prisoner.IsDead || ctx == null)
                return;

            Person byKunshu = ctx.ByKunshu;

            // ① 憎恶 → 处斩。原版只判"君主厌恶俘虏"这一个方向（kunshu->is_hate(prisoner)）。
            if (byKunshu != null && byKunshu.IsHate(prisoner))
            {
                Sango.Log.Info($"*[俘虏]{byKunshu.Name} 厌恶 {prisoner.Name}，处斩");
                ctx.Execute?.Invoke(prisoner, byKunshu);
                return;
            }

            // ② 登用。成功即入伙，不再参与后续处置。
            if (ctx.TryRecruit != null && ctx.TryRecruit(prisoner))
            {
                Sango.Log.Info($"*[俘虏]{prisoner.Name} 加入了 {ctx.ByForce?.Name} 势力");
                return;
            }

            // ③ 处斩判定（必须在登用失败之后）。
            if (byKunshu != null && Shodan2(prisoner, byKunshu))
            {
                Sango.Log.Info($"*[俘虏]{byKunshu.Name} 决定处斩 {prisoner.Name}");
                ctx.Execute?.Invoke(prisoner, byKunshu);
                return;
            }

            // ④ 势力尚存的君主 → 释放。
            // 原版只放 is_kunshu；本项目 IsActiveGovernor 额外要求势力还在
            // （势力已灭的君主此时已下野，走 ⑤/⑥）。
            if (prisoner.IsActiveGovernor)
            {
                Sango.Log.Info($"*[俘虏]{prisoner.Name} 是一方之主，予以释放");
                ctx.Release?.Invoke(prisoner);
                return;
            }

            // ⑤ 收押。
            // 【原版前置】hobaku() 第一句就是 `if (not self->remaining) return false;`
            // （:203-205）—— 目标势力已经没有任何据点时，俘虏留在手里没有意义，
            // 原版直接走"释放"。remaining 等价于"目标势力是否还有别的据点"，
            // 这里用俘虏当前所属势力的城池数判定（势力已灭时 BelongForce 为空，自然为 false）。
            bool targetForceRemains = prisoner.BelongForce != null && prisoner.BelongForce.CityCount > 0;
            if (targetForceRemains && ctx.HasDetainFacility && CanAffordHobaku(ctx))
            {
                Sango.Log.Info($"*[俘虏]{prisoner.Name} 被 {ctx.ByForce?.Name} 收押");
                ctx.Detain?.Invoke(prisoner);
                return;
            }

            // ⑥ 释放
            Sango.Log.Info($"*[俘虏]{ctx.ByForce?.Name} 无力关押 {prisoner.Name}，予以释放");
            ctx.Release?.Invoke(prisoner);
        }

        /// <summary>
        /// 处斩判定（对齐 `System::shodan2`，s11_sys_event_capture.cpp:158-199）。
        ///
        /// <code>
        /// 豁免：义兄弟 / 夫妻 / 君主亲爱此人 → 不斩
        /// 君主被俘（原版 is_kunshu）：
        ///   n = (君主野心 + 俘虏野心) * 2 - (max(最高能力,50) + max(平均能力,50)) / 2
        ///       + (16 - 君主义理) * 5 - 战死系数 - rand(20)
        /// 普通武将：
        ///   f = 100 - (max(最高能力,50) + max(平均能力,50)) / 2
        ///       - max(功绩,20000) / 2000 * (君主晋升意向==实绩 ? 1.5 : 1)
        ///       - 俘虏野心 * (君主晋升意向==义理 ? 5 : 1)
        ///       - rand(25) * (君主晋升意向==任意 ? 1.5 : 1)
        ///       - 战死系数
        /// return rand_bool(max(n, 0))
        /// </code>
        ///
        /// 【本项目无法映射的项】原版 shodan1 里"俘虏是保护后汉皇帝的势力 / 我方保护皇帝"
        /// 两条处斩分支（:62-67）没有对应机制，未实现。
        /// </summary>
        /// <param name="prisoner">俘虏</param>
        /// <param name="kunshu">裁决方君主</param>
        /// <returns>处斩返回 true</returns>
        public static bool Shodan2(Person prisoner, Person kunshu)
        {
            if (prisoner == null || kunshu == null)
                return false;

            // 豁免三条：义兄弟 / 夫妻 / 亲爱（原版 :161-168）
            if (CityStateSystem.IsSpouseOrSworn(prisoner, kunshu))
                return false;
            if (kunshu.IsLike(prisoner))
                return false;

            // 五项能力：最高值与平均值（原版遍历 PersonStatType_Max 五项）
            int maxStat = 0;
            int avgStat = 0;
            int[] stats = { prisoner.Command, prisoner.Strength, prisoner.Intelligence, prisoner.Politics, prisoner.Glamour };
            for (int i = 0; i < stats.Length; i++)
            {
                maxStat = System.Math.Max(maxStat, stats[i]);
                avgStat += stats[i];
            }
            avgStat /= stats.Length;

            // 战死系数：原版 death_coef = { 20, 10, 0 }，下标是战死频率档位
            int[] deathCoef = { 20, 10, 0 };
            int deathMode = (int)Sango.Core.Duel.DuelRules.GetBattleDeathMode();
            if (deathMode < 0) deathMode = 0;
            if (deathMode > 2) deathMode = 2;
            int death = deathCoef[deathMode];

            int n;
            if (prisoner.IsActiveGovernor)
            {
                n = (kunshu.ambition + prisoner.ambition) * 2;
                n -= (System.Math.Max(maxStat, 50) + System.Math.Max(avgStat, 50)) / 2;
                n += (16 - CityStateSystem.GetGiri(kunshu)) * 5;
                n -= death;
                n -= GameRandom.Range(0, 20);
            }
            else
            {
                // 原版 promotion 枚举：0=能力 1=实绩 2=名声 3=义理 4=任意（s11_enum_scen.h:376-384）
                int promotion = kunshu.promotion;
                float f = 0;
                f += 100 - (System.Math.Max(maxStat, 50) + System.Math.Max(avgStat, 50)) / 2;
                f -= System.Math.Max(prisoner.merit, 20000) / 2000 * (promotion == 1 ? 1.5f : 1f);
                f -= prisoner.ambition * (promotion == 3 ? 5 : 1);
                f -= GameRandom.Range(0, 25) * (promotion == 4 ? 1.5f : 1f);
                f -= death;
                n = (int)f;
            }

            return GameRandom.Chance(System.Math.Max(n, 0));
        }

        /// <summary>
        /// 赎金负担判定（对齐 `System::hobaku`，s11_sys_event_capture.cpp:201-248）。
        ///
        /// <code>
        /// pay_sum = Σ(攻方成员俸禄) + (俘虏数 + 2) * horyo_pay_
        /// pay_sum *= 2
        /// 攻方资金 &lt; pay_sum → 关押不起，只能释放
        /// </code>
        ///
        /// 俘虏数每次都重新读取（原版每次调用都重新统计所在地俘虏数），
        /// 所以连续收押会越来越贵。
        /// </summary>
        /// <param name="ctx">裁决上下文</param>
        /// <returns>关押得起返回 true</returns>
        public static bool CanAffordHobaku(HoryoShoguuContext ctx)
        {
            if (ctx == null)
                return false;

            int captiveCount = ctx.GetCaptiveCount != null ? ctx.GetCaptiveCount() : 0;
            int paySum = ctx.PaySum + (captiveCount + 2) * HoryoPay;
            paySum *= 2;
            return ctx.AvailableGold >= paySum;
        }

        /// <summary>
        /// 统计一批武将的俸禄合计（原版 get_pay_sum）。
        /// </summary>
        /// <param name="persons">武将列表</param>
        /// <returns>俸禄合计</returns>
        public static int SumPay(IList<Person> persons)
        {
            if (persons == null)
                return 0;
            int sum = 0;
            for (int i = 0; i < persons.Count; i++)
            {
                Person p = persons[i];
                if (p != null && p.Official != null)
                    sum += p.Official.cost;
            }
            return sum;
        }
    }
}
