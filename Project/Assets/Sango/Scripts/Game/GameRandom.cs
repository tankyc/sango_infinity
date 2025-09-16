using System;

namespace Sango.Game
{
    public static class GameRandom
    {
        static Random random;

        public static void Init()
        {
            random = new Random(Guid.NewGuid().GetHashCode());
        }
        public static double Random()
        {
            return random.NextDouble();
        }

        /// <summary>
        /// 给定一个基础数值,随机一定浮动比例
        /// </summary>
        /// <param name="baseV"></param>
        /// <param name="floatV"></param>
        /// <returns></returns>
        public static int Random(int baseV, float floatP)
        {
            if(baseV <= 0) return 0;
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
        public static bool Changce(int chance)
        {
            return Changce(chance, 100);
        }

        public static bool Changce(int chance, int root)
        {
            if (chance <= 0) return false;
            else if (chance >= root) return true;
            else
            {
                int rs = random.Next(root);
                return rs <= chance;
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
                return random.Next(max, min);
            else
                return random.Next(min, max);
        }
        public static int Range(int maxValue)
        {
            return random.Next(maxValue);
        }

        public static float Range(float min, float max)
        {
            if (min > max)
                return (float)random.Next((int)max * 10000, (int)min * 10000) / 10000f;
            else
                return (float)random.Next((int)min * 10000, (int)max * 10000) / 10000f;
        }
        public static float Range(float maxValue)
        {
            return (float)random.Next((int)maxValue * 10000) / 10000f;
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

    }
}
