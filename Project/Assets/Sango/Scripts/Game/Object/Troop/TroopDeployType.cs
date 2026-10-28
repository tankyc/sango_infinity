namespace Sango.Core
{
    /// <summary>
    /// 部队出征意图。
    ///
    /// 【为什么需要它】原版部队的"去哪、干什么"是**出征时就定死的意图**，存在
    /// ScenarioAI 的 <c>context_-&gt;unit[].deploy_type / deploy_target</c> 里
    /// （结构见 s11_scen.h:3495-3503），由据点级 AI 出征命令写入
    /// （s11_game_ai_kyoten_cmd_mil_attack.cpp:396-398、mil_defense.cpp:264-266、
    ///  mil_support.cpp:189-191、mil_build.cpp:112-114），部队层只读它做决策
    /// （s11_game_ai_unit.cpp:124-130）。
    ///
    /// 没有意图层就无法复刻原版的改派表——同样是"移动中"的部队，
    /// Attack 意图和 Transport 意图在 order 失效后的走向完全不同。
    /// </summary>
    public enum TroopDeployType
    {
        /// <summary>无意图（原版 DeployType_None）：只在自身领地内活动</summary>
        None = 0,

        /// <summary>进攻（原版 DeployType_Attack）：目标是敌方或中立据点</summary>
        Attack = 1,

        /// <summary>防卫（原版 DeployType_Defense）：目标是己方受威胁据点</summary>
        Defense = 2,

        /// <summary>输送（原版 DeployType_Transport）：目标是己方据点，运兵/运粮/运兵装</summary>
        Transport = 3,

        /// <summary>建设（原版 DeployType_Build）：随军工程队，目标是建设地点</summary>
        Build = 4,
    }
}
