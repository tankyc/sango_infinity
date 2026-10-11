using System;
using UnityEngine;

namespace Sango.MapGen
{
    /// <summary>
    /// 程序化地图生成所使用的噪声与统计工具。
    /// 基于 UnityEngine.Mathf.PerlinNoise 封装分形噪声(fBm)，并提供分位数(quantile)统计，
    /// 用于把噪声场转换为"稳定比例"的地形判定阈值。
    /// </summary>
    public static class MapNoise
    {
        /// <summary>
        /// 分形布朗运动(fBm)噪声，返回约 0~1 的连续值。
        /// </summary>
        /// <param name="x">采样点 X 坐标（已缩放到噪声空间）</param>
        /// <param name="y">采样点 Y 坐标（已缩放到噪声空间）</param>
        /// <param name="octaves">倍频数</param>
        /// <param name="persistence">振幅衰减系数</param>
        /// <param name="lacunarity">频率增长系数</param>
        /// <param name="offsetX">X 方向偏移，用于同种子下产生互不相关的噪声</param>
        /// <param name="offsetY">Y 方向偏移，用于同种子下产生互不相关的噪声</param>
        /// <returns>约 0~1 的噪声值</returns>
        public static float FBM(float x, float y, int octaves, float persistence, float lacunarity, float offsetX, float offsetY)
        {
            // 逐倍频叠加，每次频率升高、振幅降低
            float amplitude = 1f;
            float frequency = 1f;
            float sum = 0f;
            float norm = 1e-6f;

            int oc = Mathf.Max(1, octaves);
            for (int i = 0; i < oc; i++)
            {
                // 每个倍频额外加一个固定偏移，避免所有倍频在同一点取到相同的采样值
                sum += Mathf.PerlinNoise(x * frequency + offsetX + i * 13.37f,
                                         y * frequency + offsetY + i * 7.77f) * amplitude;
                norm += amplitude;
                amplitude *= persistence;
                frequency *= lacunarity;
            }

            return sum / norm;
        }

        /// <summary>
        /// 依据种子生成一组稳定的噪声偏移量。
        /// </summary>
        /// <param name="seed">随机种子</param>
        /// <param name="offsetX">输出的 X 偏移</param>
        /// <param name="offsetY">输出的 Y 偏移</param>
        public static void MakeSeedOffset(int seed, out float offsetX, out float offsetY)
        {
            // 使用 System.Random 保证跨平台、跨次运行结果一致
            System.Random random = new System.Random(seed);
            offsetX = (float)(random.NextDouble() * 10000.0);
            offsetY = (float)(random.NextDouble() * 10000.0);
        }

        /// <summary>
        /// 计算一组数值的分位数(quantile)。
        /// 用于把"归一化比例"映射为噪声场上的具体阈值，保证地形占比稳定。
        /// </summary>
        /// <param name="values">样本数组（不会被修改）</param>
        /// <param name="q">分位比例，0~1</param>
        /// <returns>对应的分位数值；样本为空时返回 0</returns>
        public static float Quantile(float[] values, float q)
        {
            if (values == null || values.Length == 0)
                return 0f;

            float[] copy = new float[values.Length];
            Array.Copy(values, copy, values.Length);
            Array.Sort(copy);

            int index = Mathf.Clamp(Mathf.RoundToInt(q * (copy.Length - 1)), 0, copy.Length - 1);
            return copy[index];
        }
    }
}
