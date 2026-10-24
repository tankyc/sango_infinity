using Sango.Core.Action;
using Sango.Core.Tools;
using System;
using System.Collections.Generic;
using System.Globalization;
using UnityEngine;

namespace Sango.Core
{
    public class GameUtility
    {

        //public static OverrideData<int> IntOverrideData = new OverrideData<int>(0);
        //public static OverrideData<int> IntOverrideData2 = new OverrideData<int>(0);
        //public static OverrideData<int> IntOverrideData3 = new OverrideData<int>(0);
        //public static OverrideData<float> FloatOverrideData = new OverrideData<float>(0);
        //public static OverrideData<float> FloatOverrideData2 = new OverrideData<float>(0);
        //public static OverrideData<float> FloatOverrideData3 = new OverrideData<float>(0);

        public static System.Random RandomDigit = new System.Random();
        static int[] v_factor = new int[] { 0, 100, 120, 150 };

        public static int Random(int maxValue)
        {
            if (maxValue <= 0)
            {
                return 0;
            }
            return RandomDigit.Next(maxValue);
        }
        public static double Random()
        {
            return RandomDigit.NextDouble();
        }
        public static int GetRandomValue(int a, int b)
        {
            int num;
            int num2;
            if (b == 0)
            {
                return 0;
            }
            if (b > 0)
            {
                num = a / b;
                num2 = a % b;
                if ((num2 > 0) && (Random(b) < num2))
                {
                    num++;
                }
                return num;
            }
            b = Math.Abs(b);
            num = a / b;
            num2 = a % b;
            if ((num2 > 0) && (Random(b) < num2))
            {
                num++;
            }
            return -num;
        }
        public static int GetBigRandomValue(int a, int b)
        {
            int num;
            int num2;
            if (b == 0)
            {
                return 0;
            }
            if (b > 0)
            {
                num = (int)(a / b);
                num2 = (int)(a % b);
                if ((num2 > 0) && (Random((int)b) < num2))
                {
                    num++;
                }
                return num;
            }
            //b = Math.Abs(b);
            b = -b;
            num = (int)(a / b);
            num2 = (int)(a % b);
            if ((num2 > 0) && (Random((int)b) < num2))
            {
                num++;
            }
            return -num;
        }
        public static bool Chance(int chance)
        {
            if (chance <= 0)
            {
                return false;
            }
            return ((chance >= 100) || (Random(100) < chance));
        }
        public static bool Chance(int chance, int root)
        {
            if (chance <= 0)
            {
                return false;
            }
            return ((chance >= root) || (Random(root) < chance));
        }
        public static int Random(int min, int max)
        {
            return Random(Math.Abs(max - min) + 1) + Math.Min(max, min);
        }
        public static int RandomGaussian(double mean, double var)
        {
            double u1 = Random();
            double u2 = Random();
            double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                         Math.Sin(2.0 * Math.PI * u2);
            return (int)Math.Round(mean + (var / 3) * randStdNormal);
        }
        public static int RandomGaussianRange(int lo, int hi)
        {
            return RandomGaussian((hi + lo) / 2.0, Math.Abs(hi - lo) / 2.0);
        }

        public static int Square(int num)
        {
            return (num * num);
        }

        public static int Method_PersonBuildAbility(Person p1, Person p2, Person p3)
        {
            int v1 = p1?.BaseBuildAbility ?? 0;
            int v2 = p2?.BaseBuildAbility ?? 0;
            int v3 = p3?.BaseBuildAbility ?? 0;

            int total = v1 + v2 + v3;
            return total * 2;
        }

        public static int Method_PersonBuildAbility(List<Person> people)
        {
            int maxValue = 0;
            for (int i = 0; i < people.Count; ++i)
            {
                Person person = people[i];
                if (person == null) continue;
                maxValue += person.BaseBuildAbility;
            }
            return maxValue * 2;
        }
        public static int Method_PersonBuildAbility(Person[] people)
        {
            int maxValue = 0;
            for (int i = 0; i < people.Length; ++i)
            {
                Person person = people[i];
                if (person == null) continue;
                maxValue += person.BaseBuildAbility;
            }
            return maxValue * 2;
        }
        public static int Method_PersonBuildAbility(SangoObjectList<Person> people)
        {
            int maxValue = 0;
            for (int i = 0; i < people.Count; ++i)
            {
                Person person = people[i];
                if (person == null) continue;
                maxValue += person.BaseBuildAbility;
            }
            return maxValue * 2;
        }

        /// <summary>
        /// 治安巡查的产出量。
        ///
        /// 对齐出处：Open311 `s11_sys_person_order_city.cpp:1365-1367`：
        /// <code>
        /// security += Σ统率 / 28 + 2
        /// 敌军来袭时 result /= 2
        /// 超过 max_public_order 时截断
        /// </code>
        /// 原实现是 `Σ v^(0.5/i) - 10`（随设施等级变化的另一套曲线），
        /// 与原版完全不同，本次整体替换 —— 推荐与执行从此走同一公式。
        /// </summary>
        /// <param name="sumCommand">执行武将的统率之和</param>
        /// <returns>治安增量（至少 1）</returns>
        public static int Method_SecurityAbility(int sumCommand)
        {
            return Math.Max(1, sumCommand / 28 + 2);
        }

        public static int Method_FarmingAbility(int v)
        {
            return Math.Max(1, (int)(Math.Pow(v, 0.5)) - 5);
        }

        public static int Method_DevelopAbility(int v)
        {
            return Math.Max(1, (int)(Math.Pow(v, 0.5)) - 5);
        }

        /// <summary>
        /// 征兵的产出量。
        ///
        /// 对齐出处：Open311 `s11_sys_scen_city.cpp:582-607`：
        /// <code>
        /// 基准 = 1000 + (治安 + 20) × Σ魅力 × 5 / 100
        /// 最终 = 基准 × 名声系数 × 兵营系数
        ///        名声系数：有特技"名声"为 1.5，否则 1
        ///        兵营系数：Lv1 1.0 / Lv2 1.2 / Lv3 1.5
        /// </code>
        /// 原实现是 `(1000+6×Σ魅力) × 兵营系数`，等价于"治安恒为 100"的特例；
        /// 本次把治安项补进来，治安下降时征兵量随之下降（原版每点 −0.5%）。
        /// <para>
        /// 【特技】"名声"的 ×1.5 **不在本方法里**：它由 <c>Features.json</c> 中
        /// Id=80 的 `CityImproveJobResult`（value=150, jobIds=[6]）通过
        /// <c>GameEvent.OnCityJobResult</c> override 结果值实现。
        /// 所有需要特技参与的内政产出都走这条 override 链，公式本身不判特技。
        /// </para>
        /// </summary>
        /// <param name="sumGlamour">执行武将的魅力之和</param>
        /// <param name="security">本城治安（0~100）</param>
        /// <param name="buildingTotalLevel">兵营等级（1~3）</param>
        /// <returns>征兵数量</returns>
        public static int Method_RecruitTroops(int sumGlamour, int security, int buildingTotalLevel)
        {
            int baseValue = 1000 + (security + 20) * sumGlamour * 5 / 100;
            return baseValue * v_factor[buildingTotalLevel] / 100;
        }

        /// <summary>征兵公式的旧签名（无治安项）保留，等价于治安 100 的特例</summary>
        /// <param name="v">执行武将的魅力之和</param>
        /// <param name="buildingTotalLevel">兵营等级</param>
        /// <returns>征兵数量</returns>
        public static int Method_RecruitTroops(int v, int buildingTotalLevel)
        {
            return Method_RecruitTroops(v, 100, buildingTotalLevel);
        }

        /// <summary>
        /// 训练的产出量（气力提升）。
        ///
        /// 对齐出处：Open311 `s11_sys_person_order_military.cpp:131-156`：
        /// <code>
        /// energy += 3 + (Σ武力 + max武力) / min(20 + troops/2000, 100)
        /// 有训练所 → 再 ×1.5
        /// 截断到 max_energy
        /// </code>
        /// 原实现是 `sqrt(Σ武力) + sqrt(max武力)`，与原版完全不同，本次整体替换。
        /// </summary>
        /// <param name="sumStrength">执行武将的武力之和</param>
        /// <param name="maxStrength">其中武力最高者的武力</param>
        /// <param name="troops">本城现有兵力（决定分母）</param>
        /// <param name="hasTrainingGround">是否有训练所（有则 ×1.5）</param>
        /// <returns>气力增量（至少 1）</returns>
        public static int Method_TrainTroops(int sumStrength, int maxStrength, int troops, bool hasTrainingGround = false)
        {
            int denom = System.Math.Min(20 + troops / 2000, 100);
            if (denom <= 0)
                denom = 1;

            int value = 3 + (sumStrength + maxStrength) / denom;
            if (hasTrainingGround)
                value = value * 3 / 2;
            return Math.Max(1, value);
        }

        // 交易比列计算公式
        public static int Method_Trade(int v)
        {
            int percent = 100;
            percent += Math.Max(0, v - 70) / 2;
            return percent;
        }



        // 这里传入的v是放大了10倍的
        public static int Method_CreateItems(int v, int buildingTotalLevel)
        {
            /*
             * 
             * 枪、戟、弩、马一次的生产数量（相关属性：智力，特技：能吏）
                （以Lv.1设施为准，LV.2设施为1.2倍，Lv.3设施为1.5倍）
                计算基准值q＝1000＋10xa＋5(xb＋xc)
                最终值Q＝β[T×α(q)]
                其中：有特技“能吏”则T为2，否则为1
                例：智力为84、83、83的三个武将，在Lv.3的设施中，一回合生产枪的数量为：
                1.5×[1000＋10×84＋5×(83＋83)]＝4005
             * 
             */
            int percent = v_factor[buildingTotalLevel];
            return percent * (v + 1000) / 100;
        }

        /// <summary>
        /// 计算建造船只所需的旬数
        /// </summary>
        /// <param name="v"></param>
        /// <param name="buildingTotalLevel"></param>
        /// <returns></returns>
        public static int Method_CreateBoatCounter(int maxValue, int subValue, int buildingTotalLevel)
        {
            /*
              舰船生产所耗回合数（相关属性：智力，特技：造船，设施：练兵所）
              计算基准值ds＝(550－2.5xa)÷30－(xb+xc)÷24
              最终值Ds＝β{[α(ds)－S]÷T}
              其中：有特技“发明”则T为2，否则为1；S为“造船厂等级/3”，最大为3
              ※舰船生产所需的回合数最大为9
              例：智力为88、72、72的三个武将，在有练兵所的情况下，生产楼船所耗旬数为：(550－2.5×88)÷30－(72＋72)÷24－2＝5－2＝3
              如果上述武将中有特技“造船”，则所耗旬数为β(3÷2)＝1
          */
            return Math.Max(1, Math.Min(9, (550 - maxValue * 10 / 4) / 30 - (subValue) / 24 - Math.Min(buildingTotalLevel / 3, 3)));
        }


        /// <summary>
        /// 计算建造器械所需的旬数
        /// </summary>
        /// <param name="v"></param>
        /// <param name="buildingTotalLevel"></param>
        /// <returns></returns>
        public static int Method_CreateMachineCounter(int maxValue, int subValue, int buildingTotalLevel)
        {
            /*
            兵器生产所耗回合数（相关属性：智力，特技：发明）
            计算基准值dv＝(550－2.5xa)÷30－(xb+xc)÷24
            最终值Dv＝β[α(dv)÷T]
            其中：有特技“发明”则T为2，否则为1
            ※兵器生产所需的回合数最大为9
            设施“练兵所”生产楼船、斗舰的时长再除以2并向上取整
            例：智力为98、95、77的三个武将，生产冲车所耗旬数为：
            (550－2.5×98)÷30－(95+77)÷24＝3
            如果上述武将中有特技为“发明”，则所耗旬数为β(3÷2)＝1
            智力为88、84、84的三个武将，生产投石所耗旬数为：
            (550－2.5×88)÷30－(84＋84)÷24＝4
          */
            return Math.Max(1, Math.Min(9, (550 - maxValue * 10 / 4) / 30 - (subValue) / 24 - Math.Min(buildingTotalLevel / 3, 3)));
        }

        public static int Method_TroopBuildAbility(Troop troop)
        {
            int value = (int)(troop.BuildPower * Math.Min(1f, (float)troop.troops / 1000) + 300 * (1f - Math.Pow(1.0 - (Math.Min(troop.troops, 5000.0) / 5000.0), 1.451)));

            // 【性格】修缮效率：按领队性格的修缮系数折算建筑耐久恢复量
            Personality PersonalityId = troop.LeaderPersonality;
            if (PersonalityId != null && PersonalityId.domesticRepairScale > 0 && PersonalityId.domesticRepairScale != 100)
            {
                long scaled = (long)value * PersonalityId.domesticRepairScale / 100;
                value = scaled > int.MaxValue ? int.MaxValue : (int)scaled;
            }

            return Math.Max(1, value);
        }

        /// <summary>
        /// 计算研究所需回合
        /// </summary>
        /// <param name="v"></param>
        /// <param name="buildingTotalLevel"></param>
        /// <returns></returns>
        public static int Method_ResearchCounter(int maxValue, int counter)
        {
            /*
            6、  研究技巧的耗旬（相关设施：人才府）
                耗旬R＝3－β[(xa＋xb＋xc)÷70]＋U－S
                其中：有设施“人才府”则S为2，否则为0
                U为技巧基础回合数
                例如：武力为76、68、66的武将，在有人才府的情况下，研究“难所行军”所耗旬数为：3－(76＋68＋66)÷70＋4－2＝2
                统率为94、93、93的武将，在有人才府的情况下，研究“袭击兵粮”所耗旬数为：3－(94＋93＋93)÷70＋4－2＝2
          */
            return Math.Max(1, 3 - maxValue / 70 + counter);
        }

        static List<ActionBase> sJobActions = new List<ActionBase>();

        public static void InitJobFeature(SangoObjectList<Person> people, params SangoObject[] sangoObjects)
        {
            ClearJobFeature();
            if (people == null) return;
            for (int i = 0; i < people.Count; ++i)
            {
                Person person = people[i];
                if (person != null && person.FeatureList != null)
                {
                    for (int j = 0; j < person.FeatureList.Count; j++)
                    {
                        Feature feature = person.FeatureList[j];
                        if (feature != null && feature.kind == (int)FeatureKindType.CityProduce)
                        {
                            if (!feature.only)
                            {
                                temp_FeatureList.Add(feature);
                                feature.InitActions(sJobActions, sangoObjects);
                            }
                            else
                            {
                                if (!temp_FeatureList.Contains(feature))
                                {
                                    temp_FeatureList.Add(feature);
                                    feature.InitActions(sJobActions, sangoObjects);
                                }
                            }
                        }
                    }
                }
            }
        }

        public static void InitJobFeature(Person[] people, params SangoObject[] sangoObjects)
        {
            ClearJobFeature();
            for (int i = 0; i < people.Length; ++i)
            {
                Person person = people[i];
                if (person != null && person.FeatureList != null)
                {
                    for (int j = 0; j < person.FeatureList.Count; j++)
                    {
                        Feature feature = person.FeatureList[j];
                        if (feature != null && feature.kind == (int)FeatureKindType.CityProduce)
                        {
                            if (!feature.only)
                            {
                                temp_FeatureList.Add(feature);
                                feature.InitActions(sJobActions, sangoObjects);
                            }
                            else
                            {
                                if (!temp_FeatureList.Contains(feature))
                                {
                                    temp_FeatureList.Add(feature);
                                    feature.InitActions(sJobActions, sangoObjects);
                                }
                            }
                        }
                    }
                }
            }
        }
        static List<Feature> temp_FeatureList = new List<Feature>();

        public static void InitJobFeature(Person person, params SangoObject[] sangoObjects)
        {
            ClearJobFeature();
            if (person != null && person.FeatureList != null)
            {
                for (int j = 0; j < person.FeatureList.Count; j++)
                {
                    Feature feature = person.FeatureList[j];
                    if (feature != null && feature.kind == (int)FeatureKindType.CityProduce)
                    {
                        if (!feature.only)
                        {
                            temp_FeatureList.Add(feature);
                            feature.InitActions(sJobActions, sangoObjects);
                        }
                        else
                        {
                            if (!temp_FeatureList.Contains(feature))
                            {
                                temp_FeatureList.Add(feature);
                                feature.InitActions(sJobActions, sangoObjects);
                            }
                        }
                    }
                }
            }
        }

        public static void ClearJobFeature()
        {
            for (int i = 0; i < sJobActions.Count; i++)
                sJobActions[i].Clear();
            sJobActions.Clear();
            temp_FeatureList.Clear();
        }
        public static void HexToColor(string hex, out Color color)
        {
            // 移除 # 或 0x 前缀
            int start = 0;
            if (hex.StartsWith("#")) start++;
            color = new Color();
            color.a = 1;
            // 解析 R、G、B、A
            byte r = byte.Parse(hex.Substring(start, 2), NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(2 + start, 2), NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(4 + start, 2), NumberStyles.HexNumber);
            color.r = r / 255f;
            color.g = g / 255f;
            color.b = b / 255f;
        }

        public static Color HexToColorA(string hex)
        {
            // 移除 # 或 0x 前缀
            if (hex.StartsWith("#")) hex = hex.Substring(1);
            if (hex.StartsWith("0x") || hex.StartsWith("0X")) hex = hex.Substring(2);

            // 默认不透明
            if (hex.Length == 6) hex = "FF" + hex; // 补全 alpha
            if (hex.Length != 8) throw new ArgumentException("十六进制颜色长度必须为 6 或 8");

            // 解析 R、G、B、A
            byte a = byte.Parse(hex.Substring(0, 2), NumberStyles.HexNumber);
            byte r = byte.Parse(hex.Substring(2, 2), NumberStyles.HexNumber);
            byte g = byte.Parse(hex.Substring(4, 2), NumberStyles.HexNumber);
            byte b = byte.Parse(hex.Substring(6, 2), NumberStyles.HexNumber);

            return new Color(r / 255f, g / 255f, b / 255f, a / 255f);
        }


        public static Color StringToColor(string colorStr)
        {
            if (string.IsNullOrEmpty(colorStr))
            {
                return new Color();
            }
            int colorInt = int.Parse(colorStr, System.Globalization.NumberStyles.AllowHexSpecifier);
            return IntToColor(colorInt);
        }

        private static Color IntToColor(int int64Value)
        {
            float basenum = 255;
            var a = System.Convert.ToByte((int64Value >> 24) & 255);
            var r = System.Convert.ToByte((int64Value >> 16) & 255);
            var g = System.Convert.ToByte((int64Value >> 8) & 255);
            var b = System.Convert.ToByte((int64Value >> 0) & 255);
            return new Color((float)r / basenum, (float)g / basenum, (float)b / basenum, (float)a / basenum);
        }

        private static int ColorToInt(Color color)
        {
            int argb = (int)(color.a * 255) << 24;
            argb += (int)(color.r * 255) << 16;
            argb += (int)(color.g * 255) << 8;
            argb += (int)(color.b * 255) << 0;
            return argb;
        }

        public static bool CheckCondition(int value, string @operator, int actualValue)
        {
            switch (@operator)
            {
                case "eq":
                    return actualValue == value;
                case "gt":
                    return actualValue > value;
                case "lt":
                    return actualValue < value;
                case "gte":
                    return actualValue >= value;
                case "lte":
                    return actualValue <= value;
                default:
                    return false;
            }
        }

        readonly static string[] endStr = new string[] { "B", "K", "M", "G" };
        public static string FormatFileSizeStr(long fileSize)
        {
            int idx = 0;
            double n = fileSize;
            while (fileSize > 1024 && idx < endStr.Length)
            {
                n = (double)fileSize / 1024f;
                fileSize = fileSize / 1024;
                idx++;
            }

            return $"{n.ToString("F2")}{endStr[idx]}";

        }

        public static bool IsValidPersonArray(Person[] personList)
        {
            if (personList == null || personList.Length == 0)
                return false;
            for (int i = 0; i < personList.Length; i++)
            {
                if (personList[i] != null)
                {
                    return true;
                }
            }
            return false;
        }
    }



}
