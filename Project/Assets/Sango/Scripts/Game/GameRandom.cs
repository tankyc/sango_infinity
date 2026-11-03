using System;

namespace Sango.Core
{
    public static class GameRandom
    {
        static Random random;

        /// <summary>
        /// 取随机源，未初始化时就地创建。
        ///
        /// 【为什么需要懒初始化】
        /// 原先 <see cref="random"/> 只在 <see cref="Init"/> 里创建，而 Init 的调用点
        /// 晚于 <c>Scenario.OnScenarioPrepare</c>：新开剧本时
        /// <c>City.OnScenarioPrepare → UpdateLife → GameRandom.Range</c> 就会掷骰，
        /// 此时 random 仍是 null，解引用直接抛 NullReferenceException，
        /// 整个剧本加载被打断（表现为"选择剧本后卡死"）。
        /// 这里做兜底，任何掷骰入口都不会再因为"忘了先 Init"而崩。
        /// </summary>
        static Random R
        {
            get
            {
                if (random == null)
                    random = new Random(Guid.NewGuid().GetHashCode());
                return random;
            }
        }

        public static void Init()
        {
            random = new Random(Guid.NewGuid().GetHashCode());
        }

        /// <summary>
        /// 用固定种子重置随机源（用于可复现的测试 / 战报回放）
        /// </summary>
        /// <param name="seed">种子</param>
        public static void Init(int seed)
        {
            random = new Random(seed);
        }

        public static double Random()
        {
            return R.NextDouble();
        }

        /// <summary>
        /// 给定一个基础数值,随机一定浮动比例
        /// </summary>
        /// <param name="baseV"></param>
        /// <param name="floatV"></param>
        /// <returns></returns>
        public static int Random(int baseV, float floatP)
        {
            if (baseV <= 0) return 0;
            int b = baseV;
            if (floatP < 1.0f)
                b = (int)(baseV * (1.0f - floatP));
            return b + Range((int)(baseV * floatP) * 2);
        }

        /// <summary>
        /// 随机一个概率1-99
        /// </summary>
        /// <param name="percent"></param>
        /// <returns></returns>
        public static bool Chance(int chance)
        {
            return Chance(chance, 100);
        }

        public static bool Chance(int chance, int root)
        {
            if (chance >= root) return true;
            else if (chance <= 0) return false;
            else
            {
                int rs = R.Next(root);
                return rs < chance;
            }
        }

        /// <summary>
        /// 返回指定范围内的随机整数（包含起始值，不包含结束值）
        /// </summary>
        /// <param name="min"></param>
        /// <param name="max"></param>
        /// <returns></returns>
        public static int Range(int min, int max)
        {
            if (min > max)
                return R.Next(max, min);
            else
                return R.Next(min, max);
        }
        public static int Range(int maxValue)
        {
            return R.Next(maxValue);
        }

        public static float Range(float min, float max)
        {
            if (min > max)
                return (float)R.Next((int)max * 10000, (int)min * 10000) / 10000f;
            else
                return (float)R.Next((int)min * 10000, (int)max * 10000) / 10000f;
        }
        public static float Range(float maxValue)
        {
            return (float)R.Next((int)maxValue * 10000) / 10000f;
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

        /// <summary>
        /// 按权重随机取下标（自动以权重总和为上界）
        /// </summary>
        /// <param name="weightValue">权重数组</param>
        /// <returns>随机到的下标</returns>
        public static int RandomWeightIndex(int[] weightValue)
        {
            if (weightValue == null || weightValue.Length == 0)
                return 0;

            int total = 0;
            for (int i = 0; i < weightValue.Length; i++)
                total += weightValue[i];

            return RandomWeightIndex(weightValue, total);
        }

        public static int RandomWeightIndex(int[] weightValue, int maxValue)
        {
            int v = Range(maxValue);
            for (int i = 0; i < weightValue.Length; i++)
            {
                if (v > weightValue[i])
                {
                    v -= weightValue[i];
                    continue;
                }
                else
                    return i;
            }
            return weightValue.Length - 1;
        }
    }
}
