namespace Sango.Core
{
    /// <summary>
    /// 原版剧本文件的解密随机数流。
    ///
    /// 【出处】D:\open311\s11\s11_file.cpp:88-135（FileRNG）
    /// 算法来自 GameCube AFA 存档格式那套"延迟 Fibonacci + XOR 洗牌"的伪随机：
    ///   · init(seed)：17 轮 LCG 播种，再按 `state[i] = (state[i-17] &lt;&lt; 23) ^ (state[i-16] &gt;&gt; 9) ^ state[i-1]`
    ///     递推填满 521 个状态，然后连续 shuffle 4 次
    ///   · next()：**先自增再取值**（第一次调用返回 state[1]，不是 state[0]），
    ///     走满 521 次再 shuffle 一轮并归零
    ///
    /// 剧本文件里每个标量值（bool/uint8/uint16/uint32/字符）解密时都 `^= next()`。
    /// 顺序错一位，后面整个文件全部乱码，所以这里必须逐行照搬。
    /// </summary>
    public class Open311FileRng
    {
        const int StateCount = 521;
        const int ShuffleSplit = 32;
        const int ShuffleGap = 489;     // StateCount - ShuffleSplit

        readonly uint[] mState = new uint[StateCount];
        int mCurrent;

        /// <summary>按文件头里的 seed 初始化</summary>
        public void Init(uint seed)
        {
            System.Array.Clear(mState, 0, StateCount);

            uint a = seed;
            uint b = 0;
            for (int i = 0; i < 17; i++)
            {
                for (int j = 0; j < 32; j++)
                {
                    a = a * 0x5d588b65u + 1u;
                    b = (b >> 1) | (a & 0x80000000u);
                }
                mState[i] = b;
            }

            mState[16] = (mState[16] << 23) ^ (mState[0] >> 9) ^ mState[15];

            for (int i = 17; i < StateCount; i++)
                mState[i] = (mState[i - 17] << 23) ^ (mState[i - 16] >> 9) ^ mState[i - 1];

            Shuffle();
            Shuffle();
            Shuffle();
            Shuffle();

            mCurrent = 0;
        }

        void Shuffle()
        {
            for (int i = 0; i < ShuffleSplit; i++)
                mState[i] ^= mState[i + ShuffleGap];

            for (int i = ShuffleSplit; i < StateCount; i++)
                mState[i] ^= mState[i - ShuffleSplit];
        }

        /// <summary>取下一个密钥字（注意：先自增再取，与原版一致）</summary>
        public uint Next()
        {
            mCurrent++;
            if (mCurrent >= StateCount)
            {
                Shuffle();
                mCurrent = 0;
            }
            return mState[mCurrent];
        }
    }
}
