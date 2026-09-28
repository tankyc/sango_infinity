
using Newtonsoft.Json;

namespace Sango.Core
{
    public class Argumentation : SangoObject
    {
        public int kind;

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
