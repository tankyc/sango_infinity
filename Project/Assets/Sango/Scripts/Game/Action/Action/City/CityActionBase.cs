using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 城市动作基类
    /// </summary>
    public abstract class CityActionBase : ActionBase
    {
        /// <summary>
        /// 城市对象
        /// </summary>
        protected City City { get; set; }
        /// <summary>
        /// 势力对象
        /// </summary>
        protected Force Force { get; set; }
        /// <summary>
        /// 参数
        /// </summary>
        protected JObject Params { get; set; }

        /// <summary>
        /// 初始化动作
        /// </summary>
        /// <param name="p">参数</param>
        /// <param name="sangoObjects">游戏对象</param>
        public override void Init(JObject p, params SangoObject[] sangoObjects)
        {
            City = sangoObjects[0] as City;
            if(City == null)
            {
                Force = sangoObjects[0] as Force;
            }
            Params = p;
        }

        public override void Execute(Trigger trigger)
        {
        }
    }
}
