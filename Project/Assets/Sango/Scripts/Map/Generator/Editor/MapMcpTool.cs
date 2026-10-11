using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Sango.MapGen.Mcp
{
    /// <summary>
    /// 面向 AI 的 MCP 自定义工具：把地图生成/编辑能力直接暴露给 AI 客户端。
    ///
    /// 使用方式(无需写 C#)：
    ///   1. 通过 MCP 工具 sango_map 调用，参数为 MapApi 指令 JSON；
    ///   2. 例如：
    ///      { "command": "{\"op\":\"generateWorld\",\"args\":{\"landRatio\":0.6,\"cityCount\":30}}" }
    ///      或 { "op": "generateWorld", "args": { "landRatio": 0.6, "cityCount": 30 } }
    ///
    /// 该文件位于 Editor 目录，随 Assembly-CSharp-Editor 编译，仅在编辑器下生效。
    /// </summary>
    [McpForUnityTool("sango_map",
        Description = "Sango Infinity 地图工具：程序化生成地形/河流/城市/领地、绘制与查询地格、保存地图。参数 command 为 MapApi 指令 JSON(可用 op=describe 查看全部操作)。",
        Group = "core",
        AutoRegister = true,
        StructuredOutput = true)]
    public static class SangoMapTool
    {
        /// <summary>
        /// 工具参数声明(用于向 AI 暴露参数 schema)。
        /// </summary>
        public class Parameters
        {
            /// <summary>MapApi 指令 JSON</summary>
            [ToolParameter("MapApi 指令 JSON，例如 {\"op\":\"generateWorld\",\"args\":{\"landRatio\":0.6}}；也可用 op=describe 获取全部操作清单", Required = false)]
            public string command { get; set; }

            /// <summary>操作名(可选)</summary>
            [ToolParameter("操作名(可选)，需与 args 搭配，例如 generateWorld", Required = false)]
            public string op { get; set; }

            /// <summary>操作参数(可选)</summary>
            [ToolParameter("操作参数(可选)，对象形式，例如 { \"landRatio\": 0.6 }", Required = false)]
            public JObject args { get; set; }
        }

        /// <summary>
        /// MCP 工具入口：转发到 MapApi。
        /// </summary>
        /// <param name="params">MCP 传入的参数对象</param>
        /// <returns>SuccessResponse 或 ErrorResponse</returns>
        public static object HandleCommand(JObject @params)
        {
            if (@params == null)
                return new ErrorResponse("参数不能为空");

            try
            {
                ToolParams p = new ToolParams(@params);
                string command = p.Get("command");

                if (string.IsNullOrEmpty(command))
                {
                    string op = p.Get("op");
                    if (string.IsNullOrEmpty(op))
                        return new ErrorResponse("需要提供 command，或同时提供 op 与 args");

                    JToken args = @params["args"];
                    string argsJson = args != null ? args.ToString(Formatting.None) : "{}";
                    command = "{\"op\":\"" + op + "\",\"args\":" + argsJson + "}";
                }

                string result = MapApi.Run(command);
                JObject response = JObject.Parse(result);

                bool ok = response.Value<bool?>("success") ?? false;
                string message = response.Value<string>("message");
                JToken data = response["data"];

                if (!ok)
                    return new ErrorResponse(string.IsNullOrEmpty(message) ? "执行失败" : message, data);

                return new SuccessResponse(string.IsNullOrEmpty(message) ? "执行成功" : message, data);
            }
            catch (System.Exception e)
            {
                return new ErrorResponse("执行异常：" + e.Message);
            }
        }
    }
}
