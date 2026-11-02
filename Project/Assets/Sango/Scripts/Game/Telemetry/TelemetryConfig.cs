/*
 * 文件名：TelemetryConfig.cs
 * 描述：玩家信息收集客户端配置（上报地址、口令、开关）。
 *
 * 【怎么用】
 *   1. 把 client/ 下四个 .cs 拷进工程，放 Assets/Sango/Scripts/Game/Telemetry/
 *      （必须和 Sango.Core 的 Game 代码同一个程序集：Framework 目录下有
 *        Sango.Framework.asmdef，那里看不到 Troop/City/Scenario，会编译不过）；
 *   2. 打开 PlayerInsight 服务端，查看 server/.config.json 里的 apiKey；
 *   3. 把下面的 IngestUrl 与 ApiKey 改成实际值，重新出包即可。
 *
 * 【为什么用常量而不是 ScriptableObject】
 *   这是纯运维开关，不需要策划在 Inspector 里调；写成常量能保证
 *   各平台包的行为完全一致，也避免多一个资源文件被误改。
 */

namespace Sango.Core.Telemetry
{
    /// <summary>
    /// 上报配置。改这里需要重新出包。
    /// </summary>
    public static class TelemetryConfig
    {
        /// <summary>
        /// 服务端地址（不含结尾斜杠）。
        /// 当前为线上环境：腾讯云轻量应用服务器 成都（lhins-dxbuxfke），
        /// 服务由 systemd 单元 playerinsight.service 托管，端口 3007，明文 HTTP。
        /// 本地调试时改成 http://127.0.0.1:3007/api 即可。
        /// </summary>
        public const string IngestUrl = "http://139.155.98.66:3007/api";

        /// <summary>
        /// 上报口令，与服务器 /opt/playerinsight/server/.config.json 的 apiKey 一致。
        /// 留空则客户端不会发送任何请求（等于关闭）。
        /// 换服务器后必须同步：登录服务器 `cat /opt/playerinsight/server/.config.json` 取 apiKey。
        /// </summary>
        public const string ApiKey = "J1DH0MgExHK1HsOyuB14Oqz5";

        /// <summary>是否采集并上报错误日志</summary>
        public static bool EnableBugReport = true;

        /// <summary>是否采集并上报玩家部队出城时的组队信息</summary>
        public static bool EnableTeamReport = true;

        /// <summary>是否同时在本地留一份日志文件（网络不通时也能让玩家手动提供）</summary>
        public static bool EnableLocalLog = true;

        /// <summary>攒批间隔（秒）。错误可能连续刷屏，攒批发能显著减少请求数</summary>
        public const float FlushInterval = 8f;

        /// <summary>单批最多发送多少条</summary>
        public const int MaxBatchPerRequest = 20;

        /// <summary>待发送队列上限，超出后丢弃最旧的（防止断网期间无限涨）</summary>
        public const int MaxQueue = 200;

        /// <summary>相同错误的合并窗口（秒），窗口内重复出现只累加次数</summary>
        public const float DedupeWindow = 30f;

        /// <summary>本地日志保留天数</summary>
        public const int LocalLogKeepDays = 7;

        /// <summary>配置是否可用（地址与口令都填了）</summary>
        public static bool IsConfigured
        {
            get
            {
                return !string.IsNullOrEmpty(IngestUrl)
                    && !string.IsNullOrEmpty(ApiKey);
            }
        }
    }
}
