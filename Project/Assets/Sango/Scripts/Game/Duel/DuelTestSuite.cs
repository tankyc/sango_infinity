/*
 * 文件名：DuelTestSuite.cs
 * 描述：单挑(Duel)系统完整测试用例集
 *
 * 使用方式：
 *   // Unity 中，在菜单 Sango/Duel/Run Test Suite 点击运行
 *   // 或代码中：
 *   DuelTestSuite.Log = Debug.Log;
 *   bool ok = DuelTestSuite.RunAll(out string summary);
 *   Debug.Log(summary);
 *
 * 覆盖内容：
 *   A. 常量与静态表      B. 计算函数          C. 适配层（性格 / 武将ID）
 *   D. 数据有效性与边界  E. 集成流程          F. 健壮性与随机压测
 *
 * 说明：
 *   所有用例均不依赖表现层（view = false），可在纯逻辑环境（含 CI）下运行。
 */

using System;
using System.Collections.Generic;
using System.Text;

namespace Sango.Core.Duel
{
    /// <summary>断言失败异常</summary>
    public class DuelAssertException : Exception
    {
        public DuelAssertException(string message) : base(message) { }
    }

    /// <summary>单条用例结果</summary>
    public class DuelTestResult
    {
        public string Name;
        public bool Passed;
        public string Detail;

        public override string ToString()
        {
            return (Passed ? "[通过] " : "[失败] ") + Name + (Detail.Length > 0 ? "  " + Detail : "");
        }
    }

    /// <summary>
    /// 单挑系统测试套件。
    /// </summary>
    public static class DuelTestSuite
    {
        #region 日志与入口

        /// <summary>日志输出（Unity 中建议设为 Debug.Log）</summary>
        public static Action<string> Log = text => Console.WriteLine(text);

        /// <summary>
        /// 运行全部用例
        /// </summary>
        /// <param name="summary">汇总文本</param>
        /// <returns>是否全部通过</returns>
        public static bool RunAll(out string summary)
        {
            List<DuelTestResult> results = RunAll();

            int passed = 0;
            for (int i = 0; i < results.Count; i++)
                if (results[i].Passed) passed++;

            StringBuilder sb = new StringBuilder();
            sb.AppendLine("===== 单挑系统测试套件 =====");
            for (int i = 0; i < results.Count; i++)
            {
                sb.AppendLine(results[i].ToString());
                Log(results[i].ToString());
            }
            sb.AppendLine($"----- 合计 {results.Count} 项，通过 {passed} 项，失败 {results.Count - passed} 项 -----");

            summary = sb.ToString();
            Log(sb.ToString());
            return passed == results.Count;
        }

        /// <summary>运行全部用例，返回逐项结果</summary>
        public static List<DuelTestResult> RunAll()
        {
            // 保证用例之间互不干扰
            DuelSettings.Reset();

            List<DuelTestResult> results = new List<DuelTestResult>();

            // ---- A. 常量与静态表 ----
            Run(results, "A01 常量取值", TestConstants);
            Run(results, "A02 行动方针系数表", TestStanceCoef);
            Run(results, "A03 必杀斗志消耗表", TestSpecialSpiritCost);

            // ---- B. 计算函数 ----
            Run(results, "B01 攻击比例范围", TestActionRatioRange);
            Run(results, "B02 普通伤害下限", TestAttackDamageFloor);
            Run(results, "B03 必杀伤害上限与增益类为0", TestSpecialDamage);
            Run(results, "B04 必杀命中次数", TestSpecialHitCount);
            Run(results, "B05 斗志获取", TestSpiritGain);
            Run(results, "B06 最低体力随性格变化", TestDuelMinHp);
            Run(results, "B07 必杀可用性判定", TestSpecialEnabled);
            Run(results, "B08 体力与斗志钳制", TestClamp);
            Run(results, "B09 伤病钳制", TestInjuryLevelClamp);

            // ---- C. 适配层 ----
            Run(results, "C01 性格自动转换", TestPersonalityMapping);

            // ---- D. 有效性与边界 ----
            Run(results, "D01 行动数据有效性校验", TestIsValid);
            Run(results, "D02 动画队列边界", TestAnimQueueBound);
            Run(results, "D03 工具函数 InRange/SetBits", TestUtils);

            // ---- E. 集成流程 ----
            Run(results, "E01 基础1v1流程", TestBasicFlow);
            Run(results, "E02 相同种子可复现", TestDeterminism);
            Run(results, "E03 不同种子结果有差异", TestSeedSensitivity);
            Run(results, "E04 3v3流程", TestThreeVsThree);
            Run(results, "E05 合数上限判平局", TestDrawByBlowLimit);
            Run(results, "E06 强制一击必杀", TestForcedFtk);
            Run(results, "E07 实力优势方胜率占优", TestStrengthAdvantage);

            // ---- F. 健壮性与压测 ----
            Run(results, "F01 伤病未初始化不崩溃", TestUninitializedInjuryLevel);
            Run(results, "F02 缺少武将时初始化安全", TestMissingPerson);
            Run(results, "F03 随机压测200局", TestStress);

            return results;
        }

        private static void Run(List<DuelTestResult> results, string name, System.Action test)
        {
            DuelTestResult result = new DuelTestResult { Name = name, Detail = "" };
            try
            {
                test();
                result.Passed = true;
            }
            catch (DuelAssertException ex)
            {
                result.Passed = false;
                result.Detail = "断言失败：" + ex.Message;
            }
            catch (Exception ex)
            {
                result.Passed = false;
                result.Detail = "异常：" + ex.GetType().Name + " " + ex.Message;
            }
            results.Add(result);
        }

        #endregion

        #region 断言工具

        private static void Assert(bool condition, string message)
        {
            if (!condition) throw new DuelAssertException(message);
        }

        private static void AssertEqual(int expected, int actual, string message)
        {
            if (expected != actual)
                throw new DuelAssertException($"{message}（期望 {expected}，实际 {actual}）");
        }

        private static void AssertRange(int value, int min, int max, string message)
        {
            if (value < min || value > max)
                throw new DuelAssertException($"{message}（值 {value} 超出 [{min}, {max}]）");
        }

        /// <summary>造一个测试武将</summary>
        private static Person P(string name, int strength, int id, int PersonalityId = 2)
        {
            return DuelTestInstance.CreatePerson(name, strength, id, PersonalityId: PersonalityId);
        }

        /// <summary>构造并初始化一次单挑（不运行）</summary>
        private static DuelTestInstance Make(Person a, Person b, int seed = 1, int maxBlow = 50, int charaCount = 1)
        {
            DuelTestInstance t = new DuelTestInstance(a, b, seed, maxBlow, charaCount);
            t.Verbose = false;
            t.OnLog = s => { };
            t.Init();
            return t;
        }

        /// <summary>跑完整场并返回结果</summary>
        private static DuelTestInstance Play(Person a, Person b, int seed = 1, int maxBlow = 50, int charaCount = 1)
        {
            DuelTestInstance t = new DuelTestInstance(a, b, seed, maxBlow, charaCount);
            t.Verbose = false;
            t.OnLog = s => { };
            t.Run();
            return t;
        }

        #endregion

        #region A. 常量与静态表

        private static void TestConstants()
        {
            AssertEqual(100, Duel.MaxHP, "MaxHP");
            AssertEqual(300, Duel.MaxSpirit, "MaxSpirit");
            AssertEqual(11, Duel.MaxAnimQueueSize, "MaxAnimQueueSize");
            AssertEqual(2, Duel.MaxTeamCount, "MaxTeamCount");
            AssertEqual(3, Duel.MaxTeamCharaCount, "MaxTeamCharaCount");
            AssertEqual(1, Duel.MinStat, "MinStat");
        }

        private static void TestStanceCoef()
        {
            DuelTestInstance t = Make(P("张辽", 80, 1), P("徐晃", 80, 2));
            Duel duel = t.Duel;

            // 各方针命中/防御应在合理区间，且防御重视的 block 明显高于攻击重视
            for (int s = 0; s < (int)DuelStance.DuelStance_Max; s++)
            {
                duel.SetStance(0, 0, s);
                int hit = duel.TeamGetStanceHit(duel.GetTeam(0), 0);
                int block = duel.TeamGetStanceBlock(duel.GetTeam(0), 0);
                int speed = duel.TeamGetStanceSpeed(duel.GetTeam(0), 0);
                AssertRange(hit, 1, 1000, $"方针{s} 命中");
                AssertRange(block, 0, 100, $"方针{s} 防御");
                AssertRange(speed, 1, 1000, $"方针{s} 速度");
            }

            duel.SetStance(0, 0, (int)DuelStance.DuelStance_Attack);
            int attackBlock = duel.TeamGetStanceBlock(duel.GetTeam(0), 0);
            duel.SetStance(0, 0, (int)DuelStance.DuelStance_Defense);
            int defenseBlock = duel.TeamGetStanceBlock(duel.GetTeam(0), 0);
            Assert(defenseBlock > attackBlock, "防御重视的 block 应大于攻击重视");
        }

        private static void TestSpecialSpiritCost()
        {
            DuelTestInstance t = Make(P("张辽", 80, 1), P("徐晃", 80, 2));
            Duel duel = t.Duel;

            AssertEqual(100, duel.GetSpecialSpiritCost((int)DuelSpecial.DuelSpecial_DeadlyMove), "必杀技消耗");
            AssertEqual(100, duel.GetSpecialSpiritCost((int)DuelSpecial.DuelSpecial_FightingSpirit), "气合消耗");
            AssertEqual(100, duel.GetSpecialSpiritCost((int)DuelSpecial.DuelSpecial_Steadfast), "坚守消耗");
            AssertEqual(100, duel.GetSpecialSpiritCost((int)DuelSpecial.DuelSpecial_Retreat), "退却消耗");
            AssertEqual(200, duel.GetSpecialSpiritCost((int)DuelSpecial.DuelSpecial_WeakPoint), "急所消耗");
            AssertEqual(300, duel.GetSpecialSpiritCost((int)DuelSpecial.DuelSpecial_Peerless), "无双消耗");
            AssertEqual(0, duel.GetSpecialSpiritCost((int)DuelSpecial.DuelSpecial_HiddenWeapon), "暗器消耗");
            AssertEqual(0, duel.GetSpecialSpiritCost((int)DuelSpecial.DuelSpecial_FeintRetreat), "伪退却消耗");
        }

        #endregion

        #region B. 计算函数

        private static void TestActionRatioRange()
        {
            DuelTestInstance t = Make(P("张辽", 80, 1), P("徐晃", 60, 2));
            Duel duel = t.Duel;

            // 同队返回 0
            AssertEqual(0, duel.GetActionRatio(0, 0, 0, 0), "同队攻击比例应为 0");

            // 跨队且在 1..99
            AssertRange(duel.GetActionRatio(0, 0, 1, 0), 1, 99, "挑战方攻击比例");
            AssertRange(duel.GetActionRatio(1, 0, 0, 0), 1, 99, "应战方攻击比例");

            // 双方比例互补（和为 100）
            int a = duel.GetActionRatio(0, 0, 1, 0);
            int b = duel.GetActionRatio(1, 0, 0, 0);
            AssertEqual(100, a + b, "双方攻击比例之和");

            // 武力高的一方比例更高
            Assert(a > b, "武力高的一方攻击比例应更高");
        }

        private static void TestAttackDamageFloor()
        {
            DuelTestInstance t = Make(P("张辽", 80, 1), P("徐晃", 80, 2));
            Duel duel = t.Duel;

            for (int s = 0; s < (int)DuelStance.DuelStance_Max; s++)
            {
                duel.SetStance(0, 0, s);
                int dmg = duel.CalcAttackDamage(0, 0);
                Assert(dmg >= 3, $"普通伤害应不低于 3（方针{s} 实际 {dmg}）");
            }
        }

        private static void TestSpecialDamage()
        {
            DuelTestInstance t = Make(P("张辽", 90, 1), P("徐晃", 60, 2));
            Duel duel = t.Duel;

            // 增益类必杀不造成伤害
            Duel.SpecialAction fightingSpirit = new Duel.SpecialAction
            {
                team = 0, chara = 0, type = (int)DuelSpecial.DuelSpecial_FightingSpirit, result = 0
            };
            AssertEqual(0, duel.CalcSpecialDamage(fightingSpirit), "气合伤害应为 0");

            Duel.SpecialAction steadfast = new Duel.SpecialAction
            {
                team = 0, chara = 0, type = (int)DuelSpecial.DuelSpecial_Steadfast, result = 0
            };
            AssertEqual(0, duel.CalcSpecialDamage(steadfast), "坚守伤害应为 0");

            // 攻击类必杀有伤害且不超过上限 80
            for (int sp = 0; sp < (int)DuelSpecial.DuelSpecial_Max; sp++)
            {
                if (sp == (int)DuelSpecial.DuelSpecial_FightingSpirit || sp == (int)DuelSpecial.DuelSpecial_Steadfast)
                    continue;
                Duel.SpecialAction act = new Duel.SpecialAction { team = 0, chara = 0, type = sp, result = 0 };
                int dmg = duel.CalcSpecialDamage(act);
                AssertRange(dmg, 0, 80, $"必杀{sp} 伤害");
            }
        }

        private static void TestSpecialHitCount()
        {
            DuelTestInstance t = Make(P("张辽", 80, 1), P("徐晃", 80, 2));
            Duel duel = t.Duel;

            int[] oneHit =
            {
                (int)DuelSpecial.DuelSpecial_DeadlyMove,
                (int)DuelSpecial.DuelSpecial_WeakPoint,
                (int)DuelSpecial.DuelSpecial_Peerless,
                (int)DuelSpecial.DuelSpecial_HiddenWeapon,
                (int)DuelSpecial.DuelSpecial_FeintRetreat,
            };
            int[] zeroHit =
            {
                (int)DuelSpecial.DuelSpecial_FightingSpirit,
                (int)DuelSpecial.DuelSpecial_Steadfast,
                (int)DuelSpecial.DuelSpecial_Retreat,
            };

            for (int i = 0; i < oneHit.Length; i++)
            {
                Duel.SpecialAction act = new Duel.SpecialAction { team = 0, chara = 0, type = oneHit[i], result = 0 };
                AssertEqual(1, duel.GetSpecialHitCount(act), $"必杀{oneHit[i]} 命中次数应为 1");
            }
            for (int i = 0; i < zeroHit.Length; i++)
            {
                Duel.SpecialAction act = new Duel.SpecialAction { team = 0, chara = 0, type = zeroHit[i], result = 0 };
                AssertEqual(0, duel.GetSpecialHitCount(act), $"必杀{zeroHit[i]} 命中次数应为 0");
            }
        }

        private static void TestSpiritGain()
        {
            DuelTestInstance t = Make(P("张辽", 80, 1), P("徐晃", 80, 2));
            Duel duel = t.Duel;

            AssertEqual(0, duel.CalcSpiritGain(0, 0, true, 0), "伤害为 0 时斗志获取应为 0");
            AssertEqual(0, duel.CalcSpiritGain(0, 0, true, -5), "伤害为负时斗志获取应为 0");

            int gain = duel.CalcSpiritGain(0, 0, true, 10);
            Assert(gain >= 1, "正常伤害应获得至少 1 点斗志");

            // 体力低时斗志获取更高
            duel.SetHp(0, 0, 90);
            int gainHigh = duel.CalcSpiritGain(0, 0, true, 10);
            duel.SetHp(0, 0, 10);
            int gainLow = duel.CalcSpiritGain(0, 0, true, 10);
            Assert(gainLow > gainHigh, "体力低时斗志获取应更高");
        }

        private static void TestDuelMinHp()
        {
            AssertEqual(80, Duel.GetDuelMinHp(P("a", 80, 1, PersonalityId: 1)), "胆小最低体力 80");
            AssertEqual(70, Duel.GetDuelMinHp(P("b", 80, 2, PersonalityId: 2)), "冷静最低体力 70");
            AssertEqual(60, Duel.GetDuelMinHp(P("c", 80, 3, PersonalityId: 3)), "刚胆最低体力 60");
            AssertEqual(50, Duel.GetDuelMinHp(P("d", 80, 4, PersonalityId: 4)), "莽撞最低体力 50");
        }

        private static void TestSpecialEnabled()
        {
            DuelTestInstance t = Make(P("张辽", 80, 1), P("徐晃", 80, 2));
            Duel duel = t.Duel;

            int deadlyMove = (int)DuelSpecial.DuelSpecial_DeadlyMove;
            int feint = (int)DuelSpecial.DuelSpecial_FeintRetreat;

            // 斗志不足时不可用
            duel.SetSpirit(0, 0, 0);
            Assert(!duel.IsSpecialEnabled(0, 0, deadlyMove), "斗志不足时必杀应不可用");

            // 斗志充足时可用
            duel.SetSpirit(0, 0, Duel.MaxSpirit);
            Assert(duel.IsSpecialEnabled(0, 0, deadlyMove), "斗志充足时必杀应可用");

            // 伪退却需要合数 >= 15（同时需要有剩余次数）
            duel.SetSpirit(0, 0, Duel.MaxSpirit);
            duel.SetSpecialRemainingCount(0, 0, feint, 1);
            bool earlyNise = duel.IsSpecialEnabled(0, 0, feint);
            for (int i = 0; i < 15; i++) duel.AddBlowCounter(1);
            bool lateNise = duel.IsSpecialEnabled(0, 0, feint);
            Assert(!earlyNise, "合数不足 15 时伪退却应不可用");
            Assert(lateNise, "合数达到 15 后伪退却应可用");
        }

        private static void TestClamp()
        {
            DuelTestInstance t = Make(P("张辽", 80, 1), P("徐晃", 80, 2));
            Duel duel = t.Duel;

            // 体力钳制在 0..MaxHP
            duel.SetHp(0, 0, 50);
            duel.AddHp(0, 0, 1000);
            AssertEqual(Duel.MaxHP, duel.GetHp(0, 0), "体力上限钳制");

            duel.AddHp(0, 0, -1000);
            AssertEqual(0, duel.GetHp(0, 0), "体力下限钳制");

            // 斗志钳制在 0..MaxSpirit
            duel.SetSpirit(0, 0, 0);
            duel.AddSpirit(0, 0, 1000);
            AssertEqual(Duel.MaxSpirit, duel.GetSpirit(0, 0), "斗志上限钳制");

            duel.AddSpirit(0, 0, -1000);
            AssertEqual(0, duel.GetSpirit(0, 0), "斗志下限钳制");
        }

        private static void TestInjuryLevelClamp()
        {
            DuelTestInstance t = Make(P("张辽", 80, 1), P("徐晃", 80, 2));
            Duel duel = t.Duel;

            int max = (int)InjuryLevel.Max - 2;
            duel.AddInjuryLevel(0, 0, 100);
            AssertRange(duel.TeamGetInjuryLevel(duel.GetTeam(0), 0), 0, max, "伤病上限钳制");

            duel.AddInjuryLevel(0, 0, -100);
            AssertRange(duel.TeamGetInjuryLevel(duel.GetTeam(0), 0), 0, max, "伤病下限钳制");
        }

        #endregion

        #region C. 适配层

        private static void TestPersonalityMapping()
        {
            string[] names = { "胆小", "冷静", "刚胆", "莽撞" };
            DuelPersonality[] expected =
            {
                DuelPersonality.Timid, DuelPersonality.Calm, DuelPersonality.Bold, DuelPersonality.Reckless
            };

            for (int i = 0; i < 4; i++)
            {
                Person p = P("测试", 80, 100 + i, PersonalityId: i + 1);
                p.mPersonality = new Personality { Id = i + 1, Name = names[i], kind = i + 1 };
                AssertEqual((int)expected[i], (int)p.GetPersonality(), $"性格 {names[i]} 映射");
            }

            // mPersonality 为空时回退到 PersonalityId 字段
            Person p2 = P("测试", 80, 200, PersonalityId: 4);
            AssertEqual((int)DuelPersonality.Reckless, (int)p2.GetPersonality(), "mPersonality 为空时的回退");

            // 越界值走兜底
            Person p3 = P("测试", 80, 201, PersonalityId: 99);
            AssertEqual((int)DuelPersonalities.Fallback, (int)p3.GetPersonality(), "越界性格走兜底");
        }

        // C02 原为"武将ID姓名解析"（验证 姓名/IdMap → PersonId 枚举）。
        // 现在武将身份直接用真实 Id（Person.Id），没有解析这一步，用例随之删除；
        // "按 Id 命中行为配置"由 DuelPersonBehaviours / DebatePersonBehaviours 各自的用例覆盖。

        #endregion

        #region D. 有效性与边界

        private static void TestIsValid()
        {
            DuelTestInstance t = Make(P("张辽", 80, 1), P("徐晃", 80, 2));
            Duel duel = t.Duel;

            // 合法行动
            Duel.Action ok = new Duel.Action { team = 0, chara = 0, type = 0, result = 0 };
            Assert(duel.IsValid(ok), "合法行动应通过校验");

            // 各项越界
            Assert(!duel.IsValid(new Duel.Action { team = -1, chara = 0, type = 0, result = 0 }), "队伍越界应失败");
            Assert(!duel.IsValid(new Duel.Action { team = 0, chara = -1, type = 0, result = 0 }), "武将越界应失败");
            Assert(!duel.IsValid(new Duel.Action { team = 0, chara = 0, type = -1, result = 0 }), "行动类型越界应失败");
            Assert(!duel.IsValid(new Duel.Action { team = 0, chara = 0, type = 0, result = -1 }), "结果越界应失败");
            Assert(!duel.IsValid(new Duel.Action
            {
                team = 0, chara = 0, type = (int)DuelAction.DuelAction_Max, result = 0
            }), "行动类型等于上界应失败");

            // 必杀
            Duel.SpecialAction spOk = new Duel.SpecialAction { team = 0, chara = 0, type = 0, result = 0 };
            Assert(duel.IsValid(spOk), "合法必杀应通过校验");
            Assert(!duel.IsValid(new Duel.SpecialAction { team = 2, chara = 0, type = 0, result = 0 }), "必杀队伍越界应失败");
            Assert(!duel.IsValid(new Duel.SpecialAction
            {
                team = 0, chara = 0, type = 0, result = (int)DuelSpecialResult.DuelSpecialResult_Max
            }), "必杀结果越界应失败");
        }

        private static void TestAnimQueueBound()
        {
            DuelTestInstance t = Make(P("张辽", 80, 1), P("徐晃", 80, 2));
            Duel duel = t.Duel;

            Assert(!duel.PlayBlowAnim(-1), "负数数量应返回 false");
            Assert(!duel.PlayHpAnim(-1), "负数数量应返回 false");
            Assert(!duel.PlaySpiritAnim(-1), "负数数量应返回 false");
            Assert(!duel.PlayBlowAnim(Duel.MaxAnimQueueSize), "等于队列长度应返回 false");
            Assert(duel.PlayBlowAnim(0), "数量 0 应返回 true");
            Assert(duel.PlayBlowAnim(Duel.MaxAnimQueueSize - 1), "数量上限-1 应返回 true");
        }

        private static void TestUtils()
        {
            Assert(Utils.InRange(0, 0, 10), "下边界");
            Assert(Utils.InRange(10, 0, 10), "上边界");
            Assert(!Utils.InRange(-1, 0, 10), "低于下边界");
            Assert(!Utils.InRange(11, 0, 10), "高于上边界");

            int flags = 0;
            Utils.SetBits(ref flags, 3);
            Assert(flags == 8, "置位后应为 8");
            Assert(Utils.HasBits(flags, 3), "应检测到位 3");
            Utils.ClearBits(ref flags, 3);
            Assert(flags == 0, "清位后应为 0");

            AssertEqual(0, Utils.Clamp(-5, 0, 10), "Clamp 下限");
            AssertEqual(10, Utils.Clamp(50, 0, 10), "Clamp 上限");
            AssertEqual(5, Utils.Clamp(5, 0, 10), "Clamp 中间值");
        }

        #endregion

        #region E. 集成流程

        private static void TestBasicFlow()
        {
            DuelTestInstance t = Play(P("张辽", 95, 1), P("徐晃", 70, 2), seed: 12345);

            Assert(t.Duel.Phase == (int)DuelPhase.DuelPhase_Closing, "结束时应处于 Closing 阶段");
            AssertRange(t.Duel.BlowCounter, 1, 50, "合数");
            AssertRange(t.Duel.WinnerTeam, 0, Duel.MaxTeamCount - 1, "胜队");
            AssertRange(t.Duel.LoserTeam, 0, Duel.MaxTeamCount - 1, "败队");
            Assert(t.Duel.WinnerTeam != t.Duel.LoserTeam, "胜队与败队不能相同");
            AssertRange(t.Param.winnerChara, 0, Duel.MaxTeamCharaCount - 1, "胜者武将序号");

            // 败方体力应归零（被击倒或退却）
            AssertRange(t.Param.hp[t.Param.loserTeam][t.Param.loserChara], 0, Duel.MaxHP, "败方体力");
        }

        private static void TestDeterminism()
        {
            DuelTestInstance t1 = Play(P("张辽", 90, 1), P("徐晃", 85, 2), seed: 777);
            DuelTestInstance t2 = Play(P("张辽", 90, 1), P("徐晃", 85, 2), seed: 777);

            AssertEqual(t1.Duel.WinnerTeam, t2.Duel.WinnerTeam, "同种子胜队应一致");
            AssertEqual(t1.Duel.BlowCounter, t2.Duel.BlowCounter, "同种子合数应一致");
            AssertEqual(t1.Param.hp[0][0], t2.Param.hp[0][0], "同种子挑战方体力应一致");
            AssertEqual(t1.Param.hp[1][0], t2.Param.hp[1][0], "同种子应战方体力应一致");
        }

        private static void TestSeedSensitivity()
        {
            HashSet<string> outcomes = new HashSet<string>();
            for (int seed = 1; seed <= 20; seed++)
            {
                DuelTestInstance t = Play(P("张辽", 75, 1), P("徐晃", 75, 2), seed: seed);
                outcomes.Add(t.Duel.WinnerTeam + "_" + t.Duel.BlowCounter);
            }
            Assert(outcomes.Count > 1, $"不同种子应产生不同结果（实际只有 {outcomes.Count} 种）");
        }

        private static void TestThreeVsThree()
        {
            // 挑战方武力递减：90 / 85 / 80；应战方：80 / 75 / 70
            DuelTestInstance t = Play(P("张辽", 90, 1), P("徐晃", 80, 2), seed: 4242, charaCount: 3);

            Assert(t.Duel.Phase == (int)DuelPhase.DuelPhase_Closing, "3v3 应正常结束");
            AssertRange(t.Duel.WinnerTeam, 0, Duel.MaxTeamCount - 1, "胜队");
            AssertRange(t.Duel.BlowCounter, 1, 50, "合数");

            // 所有参战武将体力都应在合法范围
            for (int i = 0; i < Duel.MaxTeamCount; i++)
            {
                for (int j = 0; j < Duel.MaxTeamCharaCount; j++)
                    AssertRange(t.Param.hp[i][j], 0, Duel.MaxHP, $"队伍{i} 武将{j} 体力");
            }
        }

        private static void TestDrawByBlowLimit()
        {
            // 合数上限设为 1，双方武力相同且低于一击必杀阈值 → 应判平局
            DuelTestInstance t = Play(P("张辽", 70, 1), P("徐晃", 70, 2), seed: 555, maxBlow: 1);

            AssertEqual(1, t.Duel.BlowCounter, "合数应为 1");
            AssertEqual(-1, t.Param.winnerTeam, "平局时 winnerTeam 应为 -1");
            AssertEqual(-1, t.Param.loserTeam, "平局时 loserTeam 应为 -1");
        }

        private static void TestForcedFtk()
        {
            DuelTestInstance t = new DuelTestInstance(P("张辽", 95, 1), P("徐晃", 60, 2), seed: 8888);
            t.Verbose = false;
            t.OnLog = s => { };
            // 预设一击必杀：挑战方(0)、普通类型(0)
            t.Param.ftkTeam = (int)DuelTeam.DuelTeam_Challenger;
            t.Param.ftkType = (int)DuelFtkType.DuelFtkType_Normal;
            t.Run();

            AssertEqual((int)DuelTeam.DuelTeam_Challenger, t.Duel.WinnerTeam, "强制一击必杀应由挑战方获胜");
            Assert((t.Duel.Flags & DuelStatusMask.Ftk) != 0, "状态标记应包含一击必杀位");
            Assert(t.Duel.BlowCounter <= 2, $"一击必杀应在极少合数内结束（实际 {t.Duel.BlowCounter}）");
            AssertEqual(0, t.Param.hp[t.Param.loserTeam][t.Param.loserChara], "败方体力应归零");
        }

        private static void TestStrengthAdvantage()
        {
            // 挑战方明显更强时，应占多数胜场
            int strongWins = 0;
            for (int seed = 1; seed <= 30; seed++)
            {
                DuelTestInstance t = Play(P("张辽", 100, 1), P("徐晃", 55, 2), seed: seed);
                if (t.Duel.WinnerTeam == (int)DuelTeam.DuelTeam_Challenger) strongWins++;
            }
            Assert(strongWins >= 21, $"挑战方实力占优时胜率应 >= 70%（实际 {strongWins}/30）");

            // 反向：应战方明显更强时，也应占多数胜场
            int challengedWins = 0;
            for (int seed = 1; seed <= 30; seed++)
            {
                DuelTestInstance t = Play(P("张辽", 55, 1), P("徐晃", 100, 2), seed: seed);
                if (t.Duel.WinnerTeam == (int)DuelTeam.DuelTeam_Challenged) challengedWins++;
            }
            Assert(challengedWins >= 21, $"应战方实力占优时胜率应 >= 70%（实际 {challengedWins}/30）");
        }

        #endregion

        #region F. 健壮性与压测

        private static void TestUninitializedInjuryLevel()
        {
            // 复现"伤病未初始化(-1)"的场景：不应崩溃，且能正常结束
            Person a = P("张辽", 85, 1);
            Person b = P("徐晃", 80, 2);
            DuelTestInstance t = new DuelTestInstance(a, b, seed: 31337);
            t.Verbose = false;
            t.OnLog = s => { };
            for (int i = 0; i < Duel.MaxTeamCount; i++)
            {
                for (int j = 0; j < Duel.MaxTeamCharaCount; j++)
                    t.Param.injuryLevel[i][j] = -1;   // 故意不初始化
            }
            t.Run();

            Assert(t.Duel.Phase == (int)DuelPhase.DuelPhase_Closing, "伤病未初始化时也应正常结束");
        }

        private static void TestMissingPerson()
        {
            // 未设置任何武将时，初始化应安全返回，不得抛异常
            Duel.Param param = new Duel.Param();
            param.control[0] = (int)DuelControl.DuelControl_Auto;
            param.control[1] = (int)DuelControl.DuelControl_Auto;
            param.startChara[0] = 0;
            param.startChara[1] = 0;

            Duel duel = new Duel(new DuelTestSystem(), param);
            duel.Init();

            Assert(!duel.InitTeam(0), "无武将时 InitTeam 应返回 false");
            Assert(!duel.IsManual(), "无手动控制时应为 false");
            AssertEqual(-1, duel.GetCurrentChara(0), "无武将时当前武将应为 -1");
        }

        #endregion

        #region F. 健壮性与压测（续）

        private static void TestStress()
        {
            string[] names = { "张辽", "徐晃", "于禁", "张郃", "乐进", "李典" };
            int maxBlow = 50;

            for (int n = 0; n < 200; n++)
            {
                int strA = 40 + (n * 7) % 60;   // 40..99
                int strB = 40 + (n * 13) % 60;
                string nameA = names[n % names.Length];
                string nameB = names[(n + 3) % names.Length];

                DuelTestInstance t = Play(P(nameA, strA, 1), P(nameB, strB, 2), seed: 1000 + n, maxBlow: maxBlow);

                Assert(t.Duel.Phase == (int)DuelPhase.DuelPhase_Closing, $"第 {n} 局未正常结束");
                AssertRange(t.Duel.BlowCounter, 1, maxBlow, $"第 {n} 局合数越界");

                // 胜败要么同时有效（有胜负），要么同时为 -1（平局）
                bool hasWinner = Utils.InRange(t.Param.winnerTeam, 0, Duel.MaxTeamCount - 1);
                bool hasLoser = Utils.InRange(t.Param.loserTeam, 0, Duel.MaxTeamCount - 1);
                Assert(hasWinner == hasLoser, $"第 {n} 局胜败标记不一致");
                if (hasWinner)
                    Assert(t.Param.winnerTeam != t.Param.loserTeam, $"第 {n} 局胜队与败队相同");

                // 体力与伤病范围
                for (int i = 0; i < Duel.MaxTeamCount; i++)
                {
                    AssertRange(t.Param.hp[i][0], 0, Duel.MaxHP, $"第 {n} 局队伍{i} 体力越界");
                    AssertRange(t.Param.injuryLevel[i][0], 0, (int)InjuryLevel.Max - 2, $"第 {n} 局队伍{i} 伤病越界");
                }

                // 武将结局合法
                for (int i = 0; i < Duel.MaxTeamCount; i++)
                {
                    AssertRange(t.Param.result[i][0], 0, (int)DuelCharaResult.DuelCharaResult_Max - 1,
                        $"第 {n} 局队伍{i} 结局越界");
                }
            }
        }

        #endregion

        #region Unity 菜单入口

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Sango/Duel/Run Test Suite", priority = 1000)]
        private static void MenuRunAll()
        {
            Log = UnityEngine.Debug.Log;
            bool ok = RunAll(out string summary);
            if (ok)
                UnityEngine.Debug.Log("<color=green>单挑测试套件：全部通过</color>\n" + summary);
            else
                UnityEngine.Debug.LogError("<color=red>单挑测试套件：存在失败用例</color>\n" + summary);
        }
#endif

        #endregion
    }
}
