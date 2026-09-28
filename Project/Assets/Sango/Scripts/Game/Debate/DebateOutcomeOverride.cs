/*
 * 文件名：DebateOutcomeOverride.cs
 * 描述：事件舌战的结果强制覆盖——让剧本事件能指定确定的胜负
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

namespace Sango.Core.Debate
{
    /// <summary>
    /// 事件舌战的结果强制覆盖。
    ///
    /// 为什么需要它：与单挑同理，史实桥段需要**确定**的结果，而舌战的自然结算是概率性的——
    ///   · "诸葛亮舌战群儒"：孔明必须全胜，不能有概率被张昭驳倒；
    ///   · "骂死王朗"：王朗必须被驳到气绝；
    ///   · "蒋干盗书"：蒋干必须被周瑜玩弄于股掌。
    ///
    /// 覆盖点（见 <see cref="Debate"/>）：在 ClosingPhase 调 ParamSetWinner **之前**
    /// 把强制胜负写进 winner / winType，这样经验 / 功绩 / 伤病 / 技术点的结算
    /// 才会落到正确的一方（若在 ParamSetWinner 之后改，奖励就发错人了）。
    ///
    /// 不指定（winnerTeam &lt; 0）时保持自然结算，传入 null 等价于"不干预"。
    /// </summary>
    public class DebateOutcomeOverride
    {
        /// <summary>
        /// 强制胜方队伍：0 = 挑战方，1 = 应战方，-1 = 不指定（由舌战逻辑自然决出）。
        /// </summary>
        public int winnerTeam = -1;

        /// <summary>
        /// 强制胜利方式（<see cref="DebateWinType"/>）。
        /// -1 表示不指定：胜负若被改写则退化为普通胜利，胜负未变则保留原判定
        /// （避免出现"败方被判定为留情"这种自相矛盾的结算）。
        /// </summary>
        public int winType = -1;

        /// <summary>是否指定了强制胜方</summary>
        public bool IsSpecified
        {
            get { return winnerTeam >= 0; }
        }
    }
}
