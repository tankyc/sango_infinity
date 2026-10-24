
using Newtonsoft.Json;

namespace Sango.Core
{
    public class Argumentation : SangoObject
    {
        public int kind;

        /// <summary>
        /// 该义理档位的"背盟友好度阈值"：AI 只有在 `like ≤ 本值` 时才会考虑破弃同盟。
        ///
        /// 【为什么做成配置而不是公式】原版 `s11_game_ai_force_cmd_dip.cpp:161-226` 的阈值是
        /// `58 - giri*3`，但 `giri` 的量纲与原版 `get_giri()` 不完全一致（本项目 kind 是 1~5）。
        /// 把每档的具体阈值写进 `Argumentations.json`，策划可以直接调，
        /// 不必再改代码里的系数。
        ///
        /// 0 = 未配置：调用方回退到 `58 - kind*3`（保持旧行为）。
        /// </summary>
        public int discardLikeThreshold;

        ///// <summary>
        ///// 作为目标对[伪报]成功率加成
        ///// </summary>
        //public int falseReportSuccessAdd;

        ///// <summary>
        ///// 作为目标对[扰乱]成功率加成
        ///// </summary>
        //public int disturbSuccessAdd;

        ///// <summary>
        ///// 作为目标对[镇静]成功率加成
        ///// </summary>
        //public int calmdownSuccessAdd;

        ///// <summary>
        ///// 作为目标对[伏兵]成功率加成
        ///// </summary>
        //public int ambushSuccessAdd;

        ///// <summary>
        ///// 作为目标对[内讧]成功率加成
        ///// </summary>
        //public int infightingSuccessAdd;

        ///// <summary>
        ///// 作为目标对[妖术]成功率加成
        ///// </summary>
        //public int sorcerySuccessAdd;

        ///// <summary>
        ///// 作为主将释放[伪报]的暴击率加成
        ///// </summary>
        //public int falseReportCriticalAdd;

        ///// <summary>
        ///// 作为主将释放[扰乱]的暴击率加成
        ///// </summary>
        //public int disturbCriticalAdd;

        /// <summary>
        /// 作为主将释放[内讧]的暴击率加成
        /// </summary>
        public int infightingCriticalAdd;

        /// <summary>
        /// 对忠诚的影响
        /// </summary>
        public int loyaltyAdd;


    }
}
