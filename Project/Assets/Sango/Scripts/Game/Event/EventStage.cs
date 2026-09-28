/*
 * 文件名：EventStage.cs
 * 描述：演出指令——事件分镜流里的一条指令
 * 创建日期：2026-09-25
 * 最后修改：2026-09-25
 */

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Sango.Core.Event
{
    /// <summary>
    /// 一条演出指令。
    ///
    /// 事件的分镜是一条**线性指令数组**（而不是树），配合 Label / Jump 表达分支。
    /// 为什么用线性 + 跳转而不是树：
    ///   · 编辑器里可以直接上下拖拽调整顺序，树结构做不到；
    ///   · "跳过某段""回到某段"天然可表达（跳到 Label 即可）；
    ///   · 与剧本脚本（如 Ren'Py、脚本语言）的心智一致，Mod 作者容易上手。
    ///
    /// Params 保持为原始 JObject：指令种类会不断新增，若为每种指令建一个 C# 类，
    /// 每加一条指令都要改数据模型与编辑器。用 JObject 则新增指令只需加一个执行分支。
    /// 代价是失去编译期类型检查，由 EventValidator 在编辑器侧兜底。
    /// </summary>
    [JsonObject(MemberSerialization.OptIn)]
    public class EventStage
    {
        /// <summary>指令类型</summary>
        [JsonProperty] public EventOpType Op = EventOpType.End;

        /// <summary>指令参数（各指令自己解释其字段）</summary>
        [JsonProperty] public JObject Params;

        /// <summary>
        /// 是否被禁用。禁用后播放时直接跳过本指令，便于调试时临时屏蔽某几句台词，
        /// 而不必删除再重新添加（删了就要重新调顺序）。
        /// </summary>
        [JsonProperty] public bool Disabled;

        /// <summary>
        /// 编辑器的备注，不参与逻辑。用来给策划写"这段是给谁看的"。
        /// </summary>
        [JsonProperty] public string Comment;

        #region 参数读取helper（统一做空保护，避免各指令重复判空）

        /// <summary>读字符串参数</summary>
        /// <param name="name">参数名</param>
        /// <param name="defaultValue">缺省值</param>
        /// <returns>参数值</returns>
        public string GetString(string name, string defaultValue = null)
        {
            if (Params == null) return defaultValue;
            JToken token = Params[name];
            if (token == null || token.Type == JTokenType.Null) return defaultValue;
            return token.Value<string>();
        }

        /// <summary>读整型参数（支持表达式求值由 EventValue 负责，这里只取原始值）</summary>
        /// <param name="name">参数名</param>
        /// <param name="defaultValue">缺省值</param>
        /// <returns>参数值</returns>
        public int GetInt(string name, int defaultValue = 0)
        {
            if (Params == null) return defaultValue;
            JToken token = Params[name];
            if (token == null || token.Type == JTokenType.Null) return defaultValue;
            try
            {
                return token.Value<int>();
            }
            catch (System.Exception)
            {
                // 参数里写的是表达式（如 "$ActionCity.gold"）时 Value<int> 会失败，
                // 这里不报错，交给 EventValue.EvalInt 真正求值。
                return defaultValue;
            }
        }

        /// <summary>读布尔参数</summary>
        /// <param name="name">参数名</param>
        /// <param name="defaultValue">缺省值</param>
        /// <returns>参数值</returns>
        public bool GetBool(string name, bool defaultValue = false)
        {
            if (Params == null) return defaultValue;
            JToken token = Params[name];
            if (token == null || token.Type == JTokenType.Null) return defaultValue;
            try
            {
                return token.Value<bool>();
            }
            catch (System.Exception)
            {
                return defaultValue;
            }
        }

        /// <summary>读对象参数</summary>
        /// <param name="name">参数名</param>
        /// <returns>参数对象；不存在返回 null</returns>
        public JObject GetObject(string name)
        {
            if (Params == null) return null;
            return Params[name] as JObject;
        }

        /// <summary>读数组参数</summary>
        /// <param name="name">参数名</param>
        /// <returns>参数数组；不存在返回 null</returns>
        public JArray GetArray(string name)
        {
            if (Params == null) return null;
            return Params[name] as JArray;
        }

        #endregion

        /// <summary>生成便于日志排查的中文描述</summary>
        public override string ToString()
        {
            return Disabled ? $"{Op}(已禁用)" : Op.ToString();
        }
    }
}
