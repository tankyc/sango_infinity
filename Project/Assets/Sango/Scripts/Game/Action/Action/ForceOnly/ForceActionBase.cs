using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 势力动作基类，所有与势力相关的动作都继承自此类
    /// </summary>
    public abstract class ForceActionBase : ActionBase
    {
        /// <summary>
        /// 势力对象，动作所作用的势力
        /// </summary>
        protected Force Force { get; set; }

        /// <summary>
        /// 参数，动作的配置参数
        /// </summary>
        protected JObject Params { get; set; }

        /// <summary>
        /// 初始化动作
        /// </summary>
        /// <param name="p">参数对象</param>
        /// <param name="sangoObjects">相关的游戏对象，第一个对象应为势力对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            Force = sangoObjects[0] as Force;
            Params = p;
        }
        public override void Execute(Trigger trigger)
        {
        }
    }
}
