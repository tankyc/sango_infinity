using TKNewtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 建筑动作基类
    /// </summary>
    public abstract class BuildingActionBase : ActionBase
    {
        /// <summary>
        /// 势力对象
        /// </summary>
        protected Force Force { get; set; }
        /// <summary>
        /// 建筑对象
        /// </summary>
        public BuildingBase Building { get; set; }
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
            Building = sangoObjects[0] as BuildingBase;
            if (Building == null)
                Force = sangoObjects[0] as Force;
            Params = p;
        }

        public override void Execute(Trigger trigger)
        {
        }
    }
}
