using Newtonsoft.Json.Linq;

namespace Sango.Core.Action
{
    /// <summary>
    /// 建筑动作基类
    /// </summary>
    public abstract class BuildingActionBase : ActionBase
    {
        /// <summary>
        /// 势力对象（技法装配时有值，建筑装配时为 null）
        /// </summary>
        protected Force Force { get; set; }
        /// <summary>
        /// 建筑对象（建筑装配时有值，技法装配时为 null）
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
            // 防御性判断：装配方传空数组时不应抛 IndexOutOfRangeException
            if (sangoObjects == null || sangoObjects.Length == 0)
                return;

            Building = sangoObjects[0] as BuildingBase;
            if (Building == null)
                Force = sangoObjects[0] as Force;
            Params = p;
        }

        /// <summary>
        /// 读取"作用对象"参数：0 己方 / 1 敌人 / 2 所有。
        ///
        /// 历史配置里的键名拼写错误为 <c>tartgetType</c>，这里优先读正确拼写 <c>targetType</c>，
        /// 读不到再回退旧键，保证老 JSON 不需要改动就能继续生效。
        /// </summary>
        /// <param name="p">JSON参数对象</param>
        /// <returns>作用对象枚举值</returns>
        protected static int ReadTargetType(JObject p)
        {
            if (p == null)
                return 0;

            int? newKey = p.Value<int?>("targetType");
            if (newKey.HasValue)
                return newKey.Value;

            return p.Value<int?>("tartgetType") ?? 0;
        }

        public override void Execute(Trigger trigger)
        {
        }
    }
}
